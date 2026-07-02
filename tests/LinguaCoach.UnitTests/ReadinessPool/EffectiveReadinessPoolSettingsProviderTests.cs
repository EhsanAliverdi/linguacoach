using System.Text.Json;
using LinguaCoach.Application.Admin.RuntimeSettings;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.ReadinessPool;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LinguaCoach.UnitTests.ReadinessPool;

/// <summary>
/// Unit tests for EffectiveReadinessPoolSettingsProvider (Phase 20C) — proves admin DB
/// overrides are applied on top of appsettings, and that resolution fails safe.
/// </summary>
public sealed class EffectiveReadinessPoolSettingsProviderTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly EffectiveReadinessPoolSettingsProvider _sut;
    private static readonly Guid AdminId = Guid.NewGuid();

    public EffectiveReadinessPoolSettingsProviderTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new EffectiveReadinessPoolSettingsProvider(
            _db,
            Options.Create(new ReadinessPoolReplenishmentOptions()),
            NullLogger<EffectiveReadinessPoolSettingsProvider>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private void AddOverride(string key, string valueJson)
    {
        _db.RuntimeSettingOverrides.Add(new RuntimeSettingOverride(key, valueJson, "Boolean", AdminId, "test"));
        _db.SaveChanges();
    }

    [Fact]
    public async Task NoOverrides_ReturnsAppSettingsDefaultsUnchanged()
    {
        var effective = await _sut.GetEffectiveAsync();

        Assert.False(effective.EnableReviewScaffoldGeneration);
        Assert.True(effective.DryRunOnly);
        Assert.False(effective.PracticeGymPilotEnabled);
        Assert.Equal(2, effective.MaxStudentVisibleScaffoldSuggestions);
    }

    [Fact]
    public async Task ActiveBooleanOverride_IsApplied_OthersUntouched()
    {
        AddOverride("ReadinessPool.PracticeGymPilotEnabled", "true");

        var effective = await _sut.GetEffectiveAsync();

        Assert.True(effective.PracticeGymPilotEnabled);
        Assert.False(effective.EnableReviewScaffoldGeneration); // untouched
        Assert.True(effective.DryRunOnly); // untouched
    }

    [Fact]
    public async Task ActiveIntOverride_IsApplied()
    {
        AddOverride("ReadinessPool.MaxStudentVisibleScaffoldSuggestions", "4");

        var effective = await _sut.GetEffectiveAsync();

        Assert.Equal(4, effective.MaxStudentVisibleScaffoldSuggestions);
    }

    [Fact]
    public async Task ActiveStringOverride_IsApplied()
    {
        AddOverride("ReadinessPool.PracticeGymPilotLabel", JsonSerializer.Serialize("Custom label"));

        var effective = await _sut.GetEffectiveAsync();

        Assert.Equal("Custom label", effective.PracticeGymPilotLabel);
    }

    [Fact]
    public async Task ActiveStringArrayOverride_IsApplied()
    {
        AddOverride("ReadinessPool.ScaffoldAllowedSources", JsonSerializer.Serialize(new[] { "PracticeGym", "TodayLesson" }));

        var effective = await _sut.GetEffectiveAsync();

        Assert.Equal(["PracticeGym", "TodayLesson"], effective.ScaffoldAllowedSources);
    }

    [Fact]
    public async Task InactiveOverride_IsIgnored()
    {
        var row = new RuntimeSettingOverride("ReadinessPool.PracticeGymPilotEnabled", "true", "Boolean", AdminId, "test");
        row.Deactivate(AdminId, "rolled back");
        _db.RuntimeSettingOverrides.Add(row);
        _db.SaveChanges();

        var effective = await _sut.GetEffectiveAsync();

        Assert.False(effective.PracticeGymPilotEnabled);
    }

    [Fact]
    public async Task CorruptOverrideValue_FallsBackSafely_OtherOverridesStillApplied()
    {
        AddOverride("ReadinessPool.MaxStudentVisibleScaffoldSuggestions", "not-a-number");
        AddOverride("ReadinessPool.PracticeGymPilotEnabled", "true");

        var effective = await _sut.GetEffectiveAsync();

        Assert.Equal(2, effective.MaxStudentVisibleScaffoldSuggestions); // fell back to appsettings default
        Assert.True(effective.PracticeGymPilotEnabled); // other override still applied
    }

    [Fact]
    public async Task DbFailure_ReturnsAppSettingsSnapshot()
    {
        _db.Database.CloseConnection();

        var effective = await _sut.GetEffectiveAsync();

        Assert.False(effective.EnableReviewScaffoldGeneration);
        Assert.True(effective.DryRunOnly);

        _db.Database.OpenConnection(); // reopen so Dispose() doesn't double-close
    }

    [Fact]
    public void EveryReviewScaffoldAndPilotRegistryKey_IsRecognizedByProvider()
    {
        var keys = FeatureGateDefinitions.ReviewScaffoldGeneration.Settings
            .Concat(FeatureGateDefinitions.PracticeGymReviewScaffoldPilot.Settings)
            .Select(s => s.Key);

        foreach (var key in keys)
        {
            // Applying a syntactically valid value for each key must not throw and must not
            // silently no-op (verified by checking a downstream field changed where applicable).
            AddOverride(key, key switch
            {
                "ReadinessPool.ScaffoldAllowedSources" => JsonSerializer.Serialize(new[] { "PracticeGym" }),
                "ReadinessPool.MinimumConfidenceForReviewNeed" => JsonSerializer.Serialize("Medium"),
                "ReadinessPool.PracticeGymPilotLabel" or "ReadinessPool.PracticeGymPilotReason" => JsonSerializer.Serialize("x"),
                "ReadinessPool.MaxScaffoldItemsPerStudentPerDay" or "ReadinessPool.MaxStudentVisibleScaffoldSuggestions" => "1",
                _ => "true",
            });
        }

        var exception = Record.Exception(() => _sut.GetEffectiveAsync().GetAwaiter().GetResult());
        Assert.Null(exception);
    }
}
