using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminUpdateOnboardingStepHandler : IAdminUpdateOnboardingStepHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminUpdateOnboardingStepHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminOnboardingStepDto> HandleAsync(
        UpdateOnboardingStepCommand command, CancellationToken ct = default)
    {
        var step = await _db.Set<OnboardingStepDefinition>()
            .FirstOrDefaultAsync(s => s.FlowDefinitionId == command.FlowId && s.StepKey == command.StepKey, ct)
            ?? throw new OnboardingV2ValidationException($"Step '{command.StepKey}' not found in flow {command.FlowId}.");

        if (!Enum.TryParse<OnboardingStepTypeV2>(command.StepType, out var stepType))
            throw new OnboardingV2ValidationException($"Unknown step type '{command.StepType}'.");
        if (!Enum.TryParse<OnboardingStepRequirementType>(command.RequirementType, out var reqType))
            throw new OnboardingV2ValidationException($"Unknown requirement type '{command.RequirementType}'.");
        if (!Enum.TryParse<OnboardingAnswerMapping>(command.AnswerMapping, out var mapping))
            throw new OnboardingV2ValidationException($"Unknown answer mapping '{command.AnswerMapping}'.");

        // Unified Question-Schema Phase 6b: admins author Content directly via the shared editor;
        // legacy OptionsJson is derived from it for display continuity.
        var (optionsJson, _) = command.Content is not null
            ? OnboardingContentConverter.ToLegacyFields(command.Content)
            : (SerializeOptions(command.Options), (string?)null);

        step.Update(command.Title, command.Description, stepType, reqType,
            command.StepOrder, command.IsEnabled, optionsJson, mapping, command.CategoryId);

        var content = command.Content
            ?? OnboardingContentConverter.FromLegacyStep(stepType, step.Title, optionsJson, step.ValidationMetadataJson, null);
        step.SetContent(content);

        await _db.SaveChangesAsync(ct);

        return new AdminOnboardingStepDto(step.StepKey, step.Title, step.Description,
            step.StepType.ToString(), step.RequirementType.ToString(), step.AnswerMapping.ToString(),
            step.StepOrder, step.IsEnabled, command.Options ?? ParseOptions(optionsJson),
            content is not null ? QuestionContentRedactor.RedactCorrectAnswers(content) : null,
            step.CategoryId);
    }

    private static string? SerializeOptions(IReadOnlyList<OnboardingOptionDto>? options)
    {
        if (options is null || options.Count == 0) return null;
        var list = options.Select(o => new Dictionary<string, string> { ["key"] = o.Key, ["label"] = o.Label }).ToList();
        return JsonSerializer.Serialize(list);
    }

    private static IReadOnlyList<OnboardingOptionDto>? ParseOptions(string? optionsJson)
    {
        if (optionsJson is null) return null;
        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(optionsJson);
        return parsed?.Select(o => new OnboardingOptionDto(o["key"], o["label"])).ToList();
    }
}
