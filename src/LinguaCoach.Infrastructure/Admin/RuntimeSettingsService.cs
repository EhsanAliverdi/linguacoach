using System.Text.Json;
using LinguaCoach.Application.Admin.RuntimeSettings;
using LinguaCoach.Application.ReadinessPool;
using LinguaCoach.Application.Speaking;
using LinguaCoach.Application.Writing;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class RuntimeSettingsService : IRuntimeSettingsService
{
    private readonly LinguaCoachDbContext _db;
    private readonly IFeatureGateRegistry _registry;
    private readonly ReadinessPoolReplenishmentOptions _readinessPool;
    private readonly SpeakingEvaluationOptions _speaking;
    private readonly WritingEvaluationOptions _writing;

    public RuntimeSettingsService(
        LinguaCoachDbContext db,
        IFeatureGateRegistry registry,
        IOptions<ReadinessPoolReplenishmentOptions> readinessPool,
        IOptions<SpeakingEvaluationOptions> speaking,
        IOptions<WritingEvaluationOptions> writing)
    {
        _db = db;
        _registry = registry;
        _readinessPool = readinessPool.Value;
        _speaking = speaking.Value;
        _writing = writing.Value;
    }

    public async Task<IReadOnlyList<FeatureGateGroupDto>> GetAllAsync(CancellationToken ct)
    {
        var overrides = await _db.RuntimeSettingOverrides.AsNoTracking().Where(o => o.IsActive).ToListAsync(ct);
        var lessonSettings = await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct);

        return _registry.GetAllGroups()
            .Select(def => BuildGroupDto(def, overrides, lessonSettings))
            .ToList();
    }

    public async Task<FeatureGateGroupDto?> GetByKeyAsync(string groupKey, CancellationToken ct)
    {
        var def = _registry.GetGroup(groupKey);
        if (def is null) return null;

        var keys = def.Settings.Select(s => s.Key).ToList();
        var overrides = await _db.RuntimeSettingOverrides.AsNoTracking()
            .Where(o => o.IsActive && keys.Contains(o.Key)).ToListAsync(ct);
        var lessonSettings = def.BackingStore == FeatureGateBackingStore.LessonGenerationSettingsTable
            ? await _db.LessonGenerationSettings.AsNoTracking().FirstOrDefaultAsync(ct)
            : null;

        return BuildGroupDto(def, overrides, lessonSettings);
    }

    public async Task<FeatureGateGroupDto> UpdateAsync(UpdateFeatureGateGroupCommand command, CancellationToken ct)
    {
        var def = _registry.GetGroup(command.GroupKey)
            ?? throw new KeyNotFoundException($"Unknown feature gate group '{command.GroupKey}'.");

        if (def.IsReadOnly)
            throw new InvalidOperationException($"'{def.DisplayName}' is locked and read-only in this phase.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("A reason is required to change a setting.");

        if (command.Values.Count == 0)
            throw new ArgumentException("At least one setting value must be provided.");

        var normalized = new Dictionary<string, string>();
        var requiresConfirmation = false;

        foreach (var (key, raw) in command.Values)
        {
            var settingDef = def.Settings.FirstOrDefault(s => s.Key == key)
                ?? throw new KeyNotFoundException($"Unknown setting key '{key}' for group '{command.GroupKey}'.");

            if (!settingDef.IsEditableAtRuntime)
                throw new InvalidOperationException($"'{settingDef.DisplayName}' is read-only and cannot be changed at runtime.");

            if (settingDef.RequiresConfirmation) requiresConfirmation = true;

            normalized[key] = ValidateAndNormalize(settingDef, raw);
        }

        if (requiresConfirmation &&
            !string.Equals(command.ConfirmationText?.Trim(), "CONFIRM", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Type CONFIRM to change a high-risk or critical-risk setting.");
        }

        if (def.BackingStore == FeatureGateBackingStore.ReadinessPoolOverride)
            await ApplyReadinessPoolOverridesAsync(def, normalized, command.AdminUserId, command.Reason, ct);
        else if (def.BackingStore == FeatureGateBackingStore.LessonGenerationSettingsTable)
            await ApplyLessonGenerationSettingsAsync(def, normalized, command.AdminUserId, command.Reason, ct);
        else
            throw new InvalidOperationException($"'{def.DisplayName}' does not support runtime edits.");

        await _db.SaveChangesAsync(ct);

        return await GetByKeyAsync(command.GroupKey, ct)
            ?? throw new InvalidOperationException("Feature gate group vanished after update.");
    }

    public async Task<FeatureGateGroupDto> ResetAsync(ResetFeatureGateGroupCommand command, CancellationToken ct)
    {
        var def = _registry.GetGroup(command.GroupKey)
            ?? throw new KeyNotFoundException($"Unknown feature gate group '{command.GroupKey}'.");

        if (def.IsReadOnly)
            throw new InvalidOperationException($"'{def.DisplayName}' is locked and read-only in this phase.");

        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("A reason is required to reset a setting.");

        if (def.BackingStore == FeatureGateBackingStore.ReadinessPoolOverride)
        {
            var keys = def.Settings.Select(s => s.Key).ToList();
            var overrides = await _db.RuntimeSettingOverrides
                .Where(o => o.IsActive && keys.Contains(o.Key)).ToListAsync(ct);

            foreach (var o in overrides)
            {
                var oldValue = o.ValueJson;
                o.Deactivate(command.AdminUserId, command.Reason);
                _db.AdminAuditLogs.Add(new AdminAuditLog(
                    command.AdminUserId, "ResetFeatureGateOverride", "RuntimeSettingOverride",
                    entityId: o.Key,
                    oldValueJson: oldValue,
                    newValueJson: null,
                    reason: command.Reason));
            }
        }
        else if (def.BackingStore == FeatureGateBackingStore.LessonGenerationSettingsTable)
        {
            var settings = await _db.LessonGenerationSettings.FirstOrDefaultAsync(ct);
            if (settings is not null)
            {
                settings.ResetToDefaults();
                _db.AdminAuditLogs.Add(new AdminAuditLog(
                    command.AdminUserId, "ResetFeatureGateOverride", "LessonGenerationSettings",
                    entityId: def.GroupKey,
                    reason: command.Reason));
            }
        }
        else
        {
            throw new InvalidOperationException($"'{def.DisplayName}' does not support runtime edits.");
        }

        await _db.SaveChangesAsync(ct);

        return await GetByKeyAsync(command.GroupKey, ct)
            ?? throw new InvalidOperationException("Feature gate group vanished after reset.");
    }

    private async Task ApplyReadinessPoolOverridesAsync(
        FeatureGateGroupDefinition def, Dictionary<string, string> normalized, Guid adminUserId, string reason, CancellationToken ct)
    {
        var keys = normalized.Keys.ToList();
        var existing = await _db.RuntimeSettingOverrides.Where(o => keys.Contains(o.Key)).ToListAsync(ct);

        foreach (var (key, valueJson) in normalized)
        {
            var settingDef = def.Settings.First(s => s.Key == key);
            var row = existing.FirstOrDefault(o => o.Key == key);
            var oldValueJson = row is { IsActive: true } ? row.ValueJson : GetReadinessPoolCurrentJson(key);

            if (row is null)
            {
                row = new RuntimeSettingOverride(key, valueJson, settingDef.DataType.ToString(), adminUserId, reason);
                _db.RuntimeSettingOverrides.Add(row);
            }
            else
            {
                row.Apply(valueJson, adminUserId, reason);
            }

            _db.AdminAuditLogs.Add(new AdminAuditLog(
                adminUserId, "UpdateFeatureGate", "RuntimeSettingOverride",
                entityId: key,
                oldValueJson: oldValueJson,
                newValueJson: valueJson,
                reason: reason));
        }
    }

    private async Task ApplyLessonGenerationSettingsAsync(
        FeatureGateGroupDefinition def, Dictionary<string, string> normalized, Guid adminUserId, string reason, CancellationToken ct)
    {
        var settings = await _db.LessonGenerationSettings.FirstOrDefaultAsync(ct);
        var isNew = settings is null;
        settings ??= new LessonGenerationSettings();

        int GetInt(string key, int current) => normalized.TryGetValue(key, out var v) ? int.Parse(v) : current;
        bool GetBool(string key, bool current) => normalized.TryGetValue(key, out var v) ? v == "true" : current;

        var oldValues = normalized.Keys.ToDictionary(k => k, k => GetLessonGenerationCurrentJson(k, settings));

        settings.Update(
            GetInt("LessonGeneration.ReadyLessonBufferSize", settings.ReadyLessonBufferSize),
            GetInt("LessonGeneration.RefillThreshold", settings.RefillThreshold),
            GetInt("LessonGeneration.RefillBatchSize", settings.RefillBatchSize),
            GetInt("LessonGeneration.MaxGenerationAttempts", settings.MaxGenerationAttempts),
            GetInt("LessonGeneration.GenerationTimeoutSeconds", settings.GenerationTimeoutSeconds),
            GetInt("LessonGeneration.TtsTimeoutSeconds", settings.TtsTimeoutSeconds),
            GetInt("LessonGeneration.MaxConcurrentGenerationJobs", settings.MaxConcurrentGenerationJobs),
            GetInt("LessonGeneration.MaxConcurrentTtsJobs", settings.MaxConcurrentTtsJobs),
            GetBool("LessonGeneration.EnableBackgroundGeneration", settings.EnableBackgroundGeneration),
            GetBool("LessonGeneration.EnableTtsGeneration", settings.EnableTtsGeneration),
            GetInt("LessonGeneration.PracticeGymReadyExercisesPerType", settings.PracticeGymReadyExercisesPerType),
            GetInt("LessonGeneration.PracticeGymRefillThresholdPerType", settings.PracticeGymRefillThresholdPerType),
            GetInt("LessonGeneration.PracticeGymRefillCountPerType", settings.PracticeGymRefillCountPerType));

        if (isNew) _db.LessonGenerationSettings.Add(settings);

        foreach (var (key, oldValue) in oldValues)
        {
            _db.AdminAuditLogs.Add(new AdminAuditLog(
                adminUserId, "UpdateFeatureGate", "LessonGenerationSettings",
                entityId: key,
                oldValueJson: oldValue,
                newValueJson: normalized[key],
                reason: reason));
        }
    }

    private FeatureGateGroupDto BuildGroupDto(
        FeatureGateGroupDefinition def,
        IReadOnlyList<RuntimeSettingOverride> activeOverrides,
        LessonGenerationSettings? lessonSettings)
    {
        var settingDtos = def.Settings.Select(s => BuildSettingDto(s, def, activeOverrides, lessonSettings)).ToList();

        var relevantOverrides = activeOverrides.Where(o => def.Settings.Any(s => s.Key == o.Key)).ToList();
        var latestOverride = relevantOverrides
            .OrderByDescending(o => o.UpdatedAtUtc ?? o.CreatedAtUtc)
            .FirstOrDefault();

        return new FeatureGateGroupDto
        {
            GroupKey = def.GroupKey,
            DisplayName = def.DisplayName,
            Description = def.Description,
            Category = def.Category,
            IsReadOnly = def.IsReadOnly,
            RequiresRestart = def.RequiresRestart,
            ProductionChangeAllowed = def.ProductionChangeAllowed,
            Dependencies = def.Dependencies,
            WarningText = def.WarningText,
            Settings = settingDtos,
            LastChangedByUserId = latestOverride?.UpdatedByUserId?.ToString() ?? latestOverride?.CreatedByUserId.ToString(),
            LastChangedAtUtc = latestOverride?.UpdatedAtUtc ?? latestOverride?.CreatedAtUtc,
            LastChangeReason = latestOverride?.Reason,
            HasActiveOverride = relevantOverrides.Count > 0
                || (def.BackingStore == FeatureGateBackingStore.LessonGenerationSettingsTable && lessonSettings is not null),
        };
    }

    private FeatureGateSettingValueDto BuildSettingDto(
        FeatureGateSettingDefinition s,
        FeatureGateGroupDefinition def,
        IReadOnlyList<RuntimeSettingOverride> activeOverrides,
        LessonGenerationSettings? lessonSettings)
    {
        string effectiveJson;
        FeatureGateValueSource source;

        switch (def.BackingStore)
        {
            case FeatureGateBackingStore.ReadinessPoolOverride:
                var overrideRow = activeOverrides.FirstOrDefault(o => o.Key == s.Key);
                if (overrideRow is not null)
                {
                    effectiveJson = overrideRow.ValueJson;
                    source = FeatureGateValueSource.DatabaseOverride;
                }
                else
                {
                    effectiveJson = GetReadinessPoolCurrentJson(s.Key);
                    source = FeatureGateValueSource.AppSettings;
                }
                break;

            case FeatureGateBackingStore.LessonGenerationSettingsTable:
                if (lessonSettings is not null)
                {
                    effectiveJson = GetLessonGenerationCurrentJson(s.Key, lessonSettings);
                    source = FeatureGateValueSource.DatabaseOverride;
                }
                else
                {
                    effectiveJson = s.DefaultValueJson;
                    source = FeatureGateValueSource.Default;
                }
                break;

            case FeatureGateBackingStore.AppSettingsReadOnly:
                effectiveJson = GetAppSettingsReadOnlyJson(s.Key);
                source = s.Key.EndsWith("AllowObjectiveCompletion", StringComparison.Ordinal)
                    || s.Key.EndsWith("AllowCefrUpdate", StringComparison.Ordinal)
                    ? FeatureGateValueSource.Hardcoded
                    : FeatureGateValueSource.AppSettings;
                break;

            default:
                effectiveJson = s.DefaultValueJson;
                source = FeatureGateValueSource.Default;
                break;
        }

        return new FeatureGateSettingValueDto
        {
            Key = s.Key,
            DisplayName = s.DisplayName,
            Description = s.Description,
            DataType = s.DataType,
            EffectiveValueJson = effectiveJson,
            DefaultValueJson = s.DefaultValueJson,
            ValueSource = source,
            IsEditableAtRuntime = s.IsEditableAtRuntime && !def.IsReadOnly,
            IsRuntimeEffective = s.IsRuntimeEffective,
            RiskLevel = s.RiskLevel,
            RequiresConfirmation = s.RequiresConfirmation,
            MinValue = s.MinValue,
            MaxValue = s.MaxValue,
            MaxLength = s.MaxLength,
            AllowedValues = s.AllowedValues,
        };
    }

    private string GetReadinessPoolCurrentJson(string key) => key switch
    {
        "ReadinessPool.EnableReviewScaffoldGeneration" => Bool(_readinessPool.EnableReviewScaffoldGeneration),
        "ReadinessPool.DryRunOnly" => Bool(_readinessPool.DryRunOnly),
        "ReadinessPool.RequireAdminReview" => Bool(_readinessPool.RequireAdminReview),
        "ReadinessPool.MaxScaffoldItemsPerStudentPerDay" => _readinessPool.MaxScaffoldItemsPerStudentPerDay.ToString(),
        "ReadinessPool.ScaffoldAllowedSources" => JsonSerializer.Serialize(_readinessPool.ScaffoldAllowedSources),
        "ReadinessPool.AllowTodayLessonInsertion" => Bool(_readinessPool.AllowTodayLessonInsertion),
        "ReadinessPool.MinimumConfidenceForReviewNeed" => JsonSerializer.Serialize(_readinessPool.MinimumConfidenceForReviewNeed),
        "ReadinessPool.PracticeGymPilotEnabled" => Bool(_readinessPool.PracticeGymPilotEnabled),
        "ReadinessPool.PracticeGymPilotLabel" => JsonSerializer.Serialize(_readinessPool.PracticeGymPilotLabel),
        "ReadinessPool.PracticeGymPilotReason" => JsonSerializer.Serialize(_readinessPool.PracticeGymPilotReason),
        "ReadinessPool.MaxStudentVisibleScaffoldSuggestions" => _readinessPool.MaxStudentVisibleScaffoldSuggestions.ToString(),
        // AI Bank-First Teaching Architecture pilot — no typed options class, generic RuntimeSettingOverride only.
        "PracticeGymFormIoPilot.Enabled" => Bool(false),
        // Phase B2 — Activity feedback policy: no typed options class, generic RuntimeSettingOverride only.
        "ActivityFeedback.TodayPolicy" => JsonSerializer.Serialize("Optional"),
        "ActivityFeedback.PracticeGymPolicy" => JsonSerializer.Serialize("Optional"),
        _ => throw new KeyNotFoundException($"Unknown ReadinessPool key '{key}'."),
    };

    private static string GetLessonGenerationCurrentJson(string key, LessonGenerationSettings s) => key switch
    {
        "LessonGeneration.ReadyLessonBufferSize" => s.ReadyLessonBufferSize.ToString(),
        "LessonGeneration.RefillThreshold" => s.RefillThreshold.ToString(),
        "LessonGeneration.RefillBatchSize" => s.RefillBatchSize.ToString(),
        "LessonGeneration.MaxGenerationAttempts" => s.MaxGenerationAttempts.ToString(),
        "LessonGeneration.GenerationTimeoutSeconds" => s.GenerationTimeoutSeconds.ToString(),
        "LessonGeneration.MaxConcurrentGenerationJobs" => s.MaxConcurrentGenerationJobs.ToString(),
        "LessonGeneration.EnableBackgroundGeneration" => Bool(s.EnableBackgroundGeneration),
        "LessonGeneration.EnableTtsGeneration" => Bool(s.EnableTtsGeneration),
        "LessonGeneration.TtsTimeoutSeconds" => s.TtsTimeoutSeconds.ToString(),
        "LessonGeneration.MaxConcurrentTtsJobs" => s.MaxConcurrentTtsJobs.ToString(),
        "LessonGeneration.PracticeGymReadyExercisesPerType" => s.PracticeGymReadyExercisesPerType.ToString(),
        "LessonGeneration.PracticeGymRefillThresholdPerType" => s.PracticeGymRefillThresholdPerType.ToString(),
        "LessonGeneration.PracticeGymRefillCountPerType" => s.PracticeGymRefillCountPerType.ToString(),
        _ => throw new KeyNotFoundException($"Unknown LessonGeneration key '{key}'."),
    };

    private string GetAppSettingsReadOnlyJson(string key) => key switch
    {
        "Speaking.ApplyMasterySignals" => Bool(_speaking.ApplyMasterySignals),
        "Speaking.MinimumConfidenceForMasterySignal" => JsonSerializer.Serialize(_speaking.MinimumConfidenceForMasterySignal),
        "Speaking.AllowReviewSignals" => Bool(_speaking.AllowReviewSignals),
        "Speaking.AllowPositiveSignals" => Bool(_speaking.AllowPositiveSignals),
        "Speaking.AllowObjectiveCompletion" => Bool(_speaking.AllowObjectiveCompletion),
        "Speaking.AllowCefrUpdate" => Bool(_speaking.AllowCefrUpdate),
        "Writing.ApplyMasterySignals" => Bool(_writing.ApplyMasterySignals),
        "Writing.MinimumConfidenceForMasterySignal" => JsonSerializer.Serialize(_writing.MinimumConfidenceForMasterySignal),
        "Writing.AllowReviewSignals" => Bool(_writing.AllowReviewSignals),
        "Writing.AllowPositiveSignals" => Bool(_writing.AllowPositiveSignals),
        "Writing.AllowObjectiveCompletion" => Bool(_writing.AllowObjectiveCompletion),
        "Writing.AllowCefrUpdate" => Bool(_writing.AllowCefrUpdate),
        _ => throw new KeyNotFoundException($"Unknown AI signal safety key '{key}'."),
    };

    private static string Bool(bool value) => value ? "true" : "false";

    private static string ValidateAndNormalize(FeatureGateSettingDefinition def, JsonElement raw)
    {
        switch (def.DataType)
        {
            case FeatureGateDataType.Boolean:
                if (raw.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new ArgumentException($"'{def.DisplayName}' must be true or false.");
                return Bool(raw.GetBoolean());

            case FeatureGateDataType.Integer:
                if (raw.ValueKind != JsonValueKind.Number || !raw.TryGetInt32(out var intVal))
                    throw new ArgumentException($"'{def.DisplayName}' must be a whole number.");
                if (def.MinValue.HasValue && intVal < def.MinValue.Value)
                    throw new ArgumentException($"'{def.DisplayName}' must be at least {def.MinValue.Value}.");
                if (def.MaxValue.HasValue && intVal > def.MaxValue.Value)
                    throw new ArgumentException($"'{def.DisplayName}' must be at most {def.MaxValue.Value}.");
                return intVal.ToString();

            case FeatureGateDataType.String:
                if (raw.ValueKind != JsonValueKind.String)
                    throw new ArgumentException($"'{def.DisplayName}' must be text.");
                var strVal = raw.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(strVal))
                    throw new ArgumentException($"'{def.DisplayName}' must not be empty.");
                if (def.MaxLength.HasValue && strVal.Length > def.MaxLength.Value)
                    throw new ArgumentException($"'{def.DisplayName}' must be at most {def.MaxLength.Value} characters.");
                if (def.AllowedValues is not null && !def.AllowedValues.Contains(strVal))
                    throw new ArgumentException($"'{def.DisplayName}' must be one of: {string.Join(", ", def.AllowedValues)}.");
                return JsonSerializer.Serialize(strVal);

            case FeatureGateDataType.StringArray:
                if (raw.ValueKind != JsonValueKind.Array)
                    throw new ArgumentException($"'{def.DisplayName}' must be a list of values.");
                var arr = raw.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray();
                if (arr.Length == 0)
                    throw new ArgumentException($"'{def.DisplayName}' must include at least one value.");
                if (def.AllowedValues is not null)
                {
                    var invalid = arr.FirstOrDefault(v => !def.AllowedValues.Contains(v));
                    if (invalid is not null)
                        throw new ArgumentException($"'{invalid}' is not a valid value for '{def.DisplayName}'. Allowed: {string.Join(", ", def.AllowedValues)}.");
                }
                return JsonSerializer.Serialize(arr);

            default:
                throw new ArgumentException($"Unsupported data type for '{def.DisplayName}'.");
        }
    }
}
