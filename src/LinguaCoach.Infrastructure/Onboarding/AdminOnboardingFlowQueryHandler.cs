using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class AdminOnboardingFlowQueryHandler : IAdminOnboardingFlowQuery
{
    private readonly LinguaCoachDbContext _db;

    public AdminOnboardingFlowQueryHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<AdminOnboardingFlowDto?> HandleAsync(GetAdminOnboardingFlowQuery query, CancellationToken ct = default)
    {
        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .Where(f => f.IsActive)
            .FirstOrDefaultAsync(ct);

        if (flow is null) return null;

        var steps = flow.Steps
            .OrderBy(s => s.StepOrder)
            .Select(s => new AdminOnboardingStepDto(
                StepKey: s.StepKey,
                Title: s.Title,
                Description: s.Description,
                StepType: s.StepType.ToString(),
                RequirementType: s.RequirementType.ToString(),
                AnswerMapping: s.AnswerMapping.ToString(),
                StepOrder: s.StepOrder,
                IsEnabled: s.IsEnabled,
                Options: ParseOptions(s.OptionsJson),
                Content: s.Content is not null ? QuestionContentRedactor.RedactCorrectAnswers(s.Content) : null
                // AssessmentMetadataJson excluded even from admin view for now.
            ))
            .ToList();

        return new AdminOnboardingFlowDto(
            FlowId: flow.Id,
            Name: flow.Name,
            Version: flow.Version,
            IsActive: flow.IsActive,
            Steps: steps
        );
    }

    private static IReadOnlyList<OnboardingOptionDto>? ParseOptions(string? json)
    {
        if (json is null) return null;
        var parsed = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
        return parsed?
            .Select(o => new OnboardingOptionDto(o["key"], o["label"]))
            .ToList();
    }
}
