using System.Text.Json;
using LinguaCoach.Application.Admin.RuntimeSettings;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Writing;
using LinguaCoach.Infrastructure.Admin;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.UnitTests.Admin;

/// <summary>
/// Unit tests for RuntimeSettingsService using SQLite in-memory DbContext and default
/// (appsettings-shaped) options values.
/// Phase I2C: the "review-scaffold-generation" and "practice-gym-review-scaffold-pilot" groups
/// were removed along with the readiness pool — see
/// docs/reviews/2026-07-10-phase-i2c-readiness-pool-removal-review.md. Tests that previously
/// exercised the ReadinessPoolOverride backing store now use "activity-feedback-policy", the
/// only remaining group on that backing store.
/// </summary>
public sealed class RuntimeSettingsServiceTests : IDisposable
{
    private readonly LinguaCoachDbContext _db;
    private readonly RuntimeSettingsService _service;
    private static readonly Guid AdminId = Guid.NewGuid();

    public RuntimeSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<LinguaCoachDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new LinguaCoachDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _service = new RuntimeSettingsService(
            _db,
            new FeatureGateRegistryService(),
            Microsoft.Extensions.Options.Options.Create(new SpeakingEvaluationOptions()),
            Microsoft.Extensions.Options.Options.Create(new WritingEvaluationOptions()));
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    [Fact]
    public async Task GetByKey_NoOverride_ReturnsAppSettingsDefault()
    {
        var group = await _service.GetByKeyAsync("activity-feedback-policy", CancellationToken.None);

        Assert.NotNull(group);
        var setting = group!.Settings.First(s => s.Key == "ActivityFeedback.TodayPolicy");
        Assert.Equal("\"Optional\"", setting.EffectiveValueJson);
        Assert.Equal(FeatureGateValueSource.AppSettings, setting.ValueSource);
    }

    [Fact]
    public async Task Update_ValidString_CreatesActiveOverride()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "activity-feedback-policy", AdminId,
            new Dictionary<string, JsonElement> { ["ActivityFeedback.TodayPolicy"] = Json("\"Required\"") },
            "Enabling for test.", null);

        var group = await _service.UpdateAsync(command, CancellationToken.None);

        var setting = group.Settings.First(s => s.Key == "ActivityFeedback.TodayPolicy");
        Assert.Equal("\"Required\"", setting.EffectiveValueJson);
        Assert.Equal(FeatureGateValueSource.DatabaseOverride, setting.ValueSource);
        Assert.True(group.HasActiveOverride);
    }

    [Fact]
    public async Task Update_DisallowedStringValue_ThrowsArgumentException()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "activity-feedback-policy", AdminId,
            new Dictionary<string, JsonElement> { ["ActivityFeedback.TodayPolicy"] = Json("\"NotAllowed\"") },
            "Should fail.", null);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_UnknownKey_ThrowsKeyNotFoundException()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "activity-feedback-policy", AdminId,
            new Dictionary<string, JsonElement> { ["NotReal"] = Json("true") },
            "Should fail.", null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_UnknownGroup_ThrowsKeyNotFoundException()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "not-a-group", AdminId,
            new Dictionary<string, JsonElement> { ["Whatever"] = Json("true") },
            "Should fail.", null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _service.UpdateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_ReadOnlyGroup_ThrowsInvalidOperationException()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "ai-signal-safety-speaking", AdminId,
            new Dictionary<string, JsonElement> { ["Speaking.AllowCefrUpdate"] = Json("true") },
            "Should fail.", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.UpdateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_EmptyReason_ThrowsArgumentException()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "activity-feedback-policy", AdminId,
            new Dictionary<string, JsonElement> { ["ActivityFeedback.TodayPolicy"] = Json("\"Required\"") },
            "", null);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Reset_AfterOverride_RestoresAppSettingsSource()
    {
        var updateCommand = new UpdateFeatureGateGroupCommand(
            "activity-feedback-policy", AdminId,
            new Dictionary<string, JsonElement> { ["ActivityFeedback.TodayPolicy"] = Json("\"Required\"") },
            "Enable for reset test.", null);
        await _service.UpdateAsync(updateCommand, CancellationToken.None);

        var resetCommand = new ResetFeatureGateGroupCommand(
            "activity-feedback-policy", AdminId, "Rolling back for test.");
        var group = await _service.ResetAsync(resetCommand, CancellationToken.None);

        var setting = group.Settings.First(s => s.Key == "ActivityFeedback.TodayPolicy");
        Assert.Equal("\"Optional\"", setting.EffectiveValueJson);
        Assert.Equal(FeatureGateValueSource.AppSettings, setting.ValueSource);
    }

    [Fact]
    public async Task Update_LessonGenerationBuffer_PersistsToTableAndValidatesThreshold()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "lesson-generation-buffer", AdminId,
            new Dictionary<string, JsonElement>
            {
                ["LessonGeneration.ReadyLessonBufferSize"] = Json("2"),
                ["LessonGeneration.RefillThreshold"] = Json("5"), // invalid: must be < buffer size
            },
            "Should fail cross-field validation.", null);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(command, CancellationToken.None));
    }

    [Fact]
    public async Task Update_LessonGenerationBuffer_ValidValues_Persists()
    {
        var command = new UpdateFeatureGateGroupCommand(
            "lesson-generation-buffer", AdminId,
            new Dictionary<string, JsonElement> { ["LessonGeneration.ReadyLessonBufferSize"] = Json("8") },
            "Bump buffer for test.", null);

        var group = await _service.UpdateAsync(command, CancellationToken.None);
        var setting = group.Settings.First(s => s.Key == "LessonGeneration.ReadyLessonBufferSize");
        Assert.Equal("8", setting.EffectiveValueJson);
        Assert.Equal(FeatureGateValueSource.DatabaseOverride, setting.ValueSource);
    }

    [Fact]
    public async Task GetAll_ReturnsAllRegistryGroups()
    {
        var groups = await _service.GetAllAsync(CancellationToken.None);
        Assert.Equal(FeatureGateDefinitions.All.Count, groups.Count);
    }
}
