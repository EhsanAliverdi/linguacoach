using System.Text.Json;
using LinguaCoach.Application.Admin;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Admin;

public sealed class ExerciseTypeCatalogService : IExerciseTypeCatalogService
{
    private readonly LinguaCoachDbContext _db;

    public ExerciseTypeCatalogService(LinguaCoachDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExerciseTypeDefinitionDto>> ListAllAsync(CancellationToken ct = default) =>
        (await _db.ExerciseTypeDefinitions
            .OrderBy(e => e.Category)
            .ThenBy(e => e.PrimarySkill)
            .ThenBy(e => e.DisplayName)
            .ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetEnabledAsync(CancellationToken ct = default) =>
        (await _db.ExerciseTypeDefinitions
            .Where(e => e.IsEnabled)
            .OrderBy(e => e.DisplayName)
            .ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetGenerationEligibleAsync(CancellationToken ct = default) =>
        (await _db.ExerciseTypeDefinitions
            .Where(e => e.IsEnabled && e.ImplementationStatus == "ready")
            .OrderBy(e => e.DisplayName)
            .ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ExerciseTypeDefinitionDto>> GetByPrimarySkillAsync(string primarySkill, CancellationToken ct = default)
    {
        var normalized = primarySkill.Trim().ToLowerInvariant();
        return (await _db.ExerciseTypeDefinitions
            .Where(e => e.PrimarySkill == normalized)
            .OrderBy(e => e.DisplayName)
            .ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<ExerciseTypeDefinitionDto?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        var normalized = LinguaCoach.Domain.Entities.ExerciseTypeDefinition.NormalizeKey(key);
        var item = await _db.ExerciseTypeDefinitions.FirstOrDefaultAsync(e => e.Key == normalized, ct);
        return item is null ? null : ToDto(item);
    }

    public async Task<ExerciseTypeDefinitionDto> UpdateAsync(UpdateExerciseTypeDefinitionCommand command, CancellationToken ct = default)
    {
        var item = await _db.ExerciseTypeDefinitions.FirstOrDefaultAsync(e => e.Key == LinguaCoach.Domain.Entities.ExerciseTypeDefinition.NormalizeKey(command.Key), ct)
            ?? throw new InvalidOperationException($"Exercise type '{command.Key}' was not found.");

        if (command.IsEnabled.HasValue) item.SetEnabled(command.IsEnabled.Value);

        var hasCountUpdate = command.MinItemsPerPractice.HasValue || command.DefaultItemsPerPractice.HasValue
            || command.MaxItemsPerPractice.HasValue || command.MinOptionsPerItem.HasValue
            || command.DefaultOptionsPerItem.HasValue || command.MaxOptionsPerItem.HasValue;
        if (hasCountUpdate)
        {
            // Counts only. ImplementationStatus and runnable flags are never touched here.
            item.UpdateItemCounts(
                command.MinItemsPerPractice ?? item.MinItemsPerPractice,
                command.DefaultItemsPerPractice ?? item.DefaultItemsPerPractice,
                command.MaxItemsPerPractice ?? item.MaxItemsPerPractice,
                command.MinOptionsPerItem ?? item.MinOptionsPerItem,
                command.DefaultOptionsPerItem ?? item.DefaultOptionsPerItem,
                command.MaxOptionsPerItem ?? item.MaxOptionsPerItem);
        }

        await _db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    private static ExerciseTypeDefinitionDto ToDto(ExerciseTypeDefinition e) => new(
        e.Key,
        e.DisplayName,
        e.Description,
        e.PrimarySkill,
        ParseSkillsForRegistry(e.SecondarySkillsJson),
        e.Category,
        e.IsEnabled,
        e.ImplementationStatus,
        e.IsAvailableForGeneration,
        e.RendererKey,
        e.EvaluatorKey,
        e.GenerationPromptKey,
        e.LegacyActivityType?.ToString(),
        e.ExercisePatternKey,
        e.EstimatedDurationMinutes,
        e.RequiresAudio,
        e.RequiresImage,
        e.MinItemsPerPractice,
        e.DefaultItemsPerPractice,
        e.MaxItemsPerPractice,
        e.MinOptionsPerItem,
        e.DefaultOptionsPerItem,
        e.MaxOptionsPerItem);

    public static IReadOnlyList<string> ParseSkillsForRegistry(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
