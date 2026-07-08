using System.Text.Json;
using FluentAssertions;
using LinguaCoach.Application.Activity;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Infrastructure.Activity;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinguaCoach.UnitTests.Activity;

/// <summary>
/// Unit tests for ActivityFeedbackPolicyProvider (Phase B2) — proves the Optional default,
/// admin DB overrides per surface, and fail-safe resolution.
/// </summary>
public sealed class ActivityFeedbackPolicyProviderTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly ActivityFeedbackPolicyProvider _sut;
    private static readonly Guid AdminId = Guid.NewGuid();

    public ActivityFeedbackPolicyProviderTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ActivityFeedbackPolicyProvider(_db, NullLogger<ActivityFeedbackPolicyProvider>.Instance);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private void AddOverride(string key, string value)
    {
        _db.RuntimeSettingOverrides.Add(new RuntimeSettingOverride(
            key, JsonSerializer.Serialize(value), "String", AdminId, "test"));
        _db.SaveChanges();
    }

    [Fact]
    public async Task NoOverride_DefaultsToOptional_ForBothSurfaces()
    {
        var today = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.Today);
        var gym = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.PracticeGym);

        today.Policy.Should().Be(ActivityFeedbackPolicy.Optional);
        gym.Policy.Should().Be(ActivityFeedbackPolicy.Optional);
    }

    [Fact]
    public async Task ActiveOverride_IsApplied_PerSurface_OthersUntouched()
    {
        AddOverride("ActivityFeedback.TodayPolicy", "Required");

        var today = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.Today);
        var gym = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.PracticeGym);

        today.Policy.Should().Be(ActivityFeedbackPolicy.Required);
        gym.Policy.Should().Be(ActivityFeedbackPolicy.Optional); // untouched
    }

    [Fact]
    public async Task OffOverride_ForPracticeGym_IsApplied()
    {
        AddOverride("ActivityFeedback.PracticeGymPolicy", "Off");

        var gym = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.PracticeGym);

        gym.Policy.Should().Be(ActivityFeedbackPolicy.Off);
    }

    [Fact]
    public async Task InactiveOverride_IsIgnored()
    {
        var row = new RuntimeSettingOverride(
            "ActivityFeedback.TodayPolicy", JsonSerializer.Serialize("Required"), "String", AdminId, "test");
        row.Deactivate(AdminId, "rolled back");
        _db.RuntimeSettingOverrides.Add(row);
        _db.SaveChanges();

        var today = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.Today);

        today.Policy.Should().Be(ActivityFeedbackPolicy.Optional);
    }

    [Fact]
    public async Task CorruptOverrideValue_FallsBackToOptional()
    {
        AddOverride("ActivityFeedback.TodayPolicy", "NotARealPolicy");

        var today = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.Today);

        today.Policy.Should().Be(ActivityFeedbackPolicy.Optional);
    }

    [Fact]
    public async Task DbFailure_ReturnsOptionalDefault()
    {
        _db.Database.CloseConnection();

        var today = await _sut.GetEffectivePolicyAsync(ActivityFeedbackSurface.Today);

        today.Policy.Should().Be(ActivityFeedbackPolicy.Optional);

        _db.Database.OpenConnection(); // reopen so Dispose() doesn't double-close
    }
}
