using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminAddOnboardingStepHandler : IAdminAddOnboardingStepHandler
{
    private readonly LinguaCoachDbContext _db;

    public AdminAddOnboardingStepHandler(LinguaCoachDbContext db) => _db = db;

    public async Task<AdminOnboardingStepDto> HandleAsync(
        AddOnboardingStepCommand command, CancellationToken ct = default)
    {
        OnboardingStepKeyGuard.Validate(command.StepKey);

        var flowExists = await _db.OnboardingFlowDefinitions
            .AnyAsync(f => f.Id == command.FlowId, ct);
        if (!flowExists)
            throw new OnboardingV2ValidationException($"Flow {command.FlowId} not found.");

        var duplicate = await _db.Set<OnboardingStepDefinition>()
            .AnyAsync(s => s.FlowDefinitionId == command.FlowId && s.StepKey == command.StepKey, ct);
        if (duplicate)
            throw new InvalidOperationException($"Duplicate step key '{command.StepKey}' in flow.");

        if (!Enum.TryParse<OnboardingStepTypeV2>(command.StepType, out var stepType))
            throw new OnboardingV2ValidationException($"Unknown step type '{command.StepType}'.");
        if (!Enum.TryParse<OnboardingStepRequirementType>(command.RequirementType, out var reqType))
            throw new OnboardingV2ValidationException($"Unknown requirement type '{command.RequirementType}'.");
        if (!Enum.TryParse<OnboardingAnswerMapping>(command.AnswerMapping, out var mapping))
            throw new OnboardingV2ValidationException($"Unknown answer mapping '{command.AnswerMapping}'.");

        var optionsJson = SerializeOptions(command.Options);

        var step = new OnboardingStepDefinition(
            flowDefinitionId: command.FlowId,
            stepKey: command.StepKey,
            title: command.Title,
            stepType: stepType,
            requirementType: reqType,
            stepOrder: command.StepOrder,
            isEnabled: command.IsEnabled,
            description: command.Description,
            optionsJson: optionsJson,
            answerMapping: mapping);

        // Unified Question-Schema Phase 6: keep the shadow Content in sync with whatever the
        // admin just authored via Options, for the step types the shared schema covers.
        var content = OnboardingContentConverter.FromLegacyStep(stepType, step.Title, optionsJson, null, null);
        if (content is not null) step.SetContent(content);

        _db.Set<OnboardingStepDefinition>().Add(step);
        await _db.SaveChangesAsync(ct);

        return new AdminOnboardingStepDto(step.StepKey, step.Title, step.Description,
            step.StepType.ToString(), step.RequirementType.ToString(), step.AnswerMapping.ToString(),
            step.StepOrder, step.IsEnabled, command.Options,
            content is not null ? QuestionContentRedactor.RedactCorrectAnswers(content) : null);
    }

    private static string? SerializeOptions(IReadOnlyList<OnboardingOptionDto>? options)
    {
        if (options is null || options.Count == 0) return null;
        var list = options.Select(o => new Dictionary<string, string> { ["key"] = o.Key, ["label"] = o.Label }).ToList();
        return JsonSerializer.Serialize(list);
    }
}
