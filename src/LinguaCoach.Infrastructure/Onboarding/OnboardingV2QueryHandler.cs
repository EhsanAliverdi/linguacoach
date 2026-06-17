using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingV2QueryHandler : IOnboardingV2Query
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingV2QueryHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<OnboardingV2StatusDto> HandleAsync(GetOnboardingV2Query query, CancellationToken ct = default)
    {
        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .Where(f => f.IsActive)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No active onboarding flow definition found.");

        var progress = await _db.StudentOnboardingProgress
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct);

        if (progress is null)
        {
            // Lazy-create progress. Check if student already completed v1 onboarding.
            var profile = await _db.StudentProfiles
                .FirstOrDefaultAsync(sp => sp.UserId == query.UserId, ct);

            if (profile is null)
                throw new InvalidOperationException("Student profile not found.");

            if (profile.OnboardingStatus == OnboardingStatus.Complete)
            {
                // Existing v1-complete student: initialise v2 progress as already done.
                progress = StudentOnboardingProgress.CreateCompleted(query.UserId, flow.Id);
            }
            else
            {
                var firstStep = flow.Steps
                    .Where(s => s.IsEnabled)
                    .OrderBy(s => s.StepOrder)
                    .FirstOrDefault();
                progress = new StudentOnboardingProgress(query.UserId, flow.Id, firstStep?.StepKey);
            }

            _db.StudentOnboardingProgress.Add(progress);
            await _db.SaveChangesAsync(ct);
        }

        var orderedSteps = flow.Steps
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.StepOrder)
            .ToList();

        return new OnboardingV2StatusDto(
            FlowId: flow.Id,
            CurrentStepKey: progress.CurrentStepKey,
            Steps: orderedSteps.Select(MapStepToDto).ToList(),
            CompletedStepKeys: progress.CompletedStepKeys,
            PercentageComplete: progress.PercentageComplete,
            IsComplete: progress.IsComplete,
            PreliminaryCefrLevel: progress.PreliminaryCefrLevel
        );
    }

    internal static OnboardingV2StepDto MapStepToDto(OnboardingStepDefinition step)
    {
        List<OnboardingOptionDto>? options = null;
        if (step.OptionsJson is not null)
        {
            var parsed = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(step.OptionsJson);
            options = parsed?
                .Select(o => new OnboardingOptionDto(o["key"], o["label"]))
                .ToList();
        }

        OnboardingValidationMetadataDto? validation = null;
        if (step.ValidationMetadataJson is not null)
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, int?>>(step.ValidationMetadataJson);
            if (parsed is not null)
            {
                parsed.TryGetValue("maxLength", out var maxLen);
                parsed.TryGetValue("maxSelections", out var maxSel);
                parsed.TryGetValue("minSelections", out var minSel);
                validation = new OnboardingValidationMetadataDto(maxLen, maxSel, minSel);
            }
        }

        return new OnboardingV2StepDto(
            StepKey: step.StepKey,
            Title: step.Title,
            Description: step.Description,
            StepType: step.StepType.ToString(),
            RequirementType: step.RequirementType.ToString(),
            StepOrder: step.StepOrder,
            IsEnabled: step.IsEnabled,
            Options: options,
            ValidationMetadata: validation
            // AssessmentMetadataJson intentionally excluded.
        );
    }
}
