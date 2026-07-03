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

            // Required v2 preference steps (learning_goals, focus_areas, support_language) were
            // added after v1 onboarding existed — a v1-complete student may never have answered
            // them. Blindly marking v2 progress as complete for every v1-complete student left
            // these fields silently unset with no gate ever prompting for them (see Fix 6,
            // docs/reviews/2026-07-03-pilot-student-onboarding-placement-practice-live-audit.md).
            var hasLearningGoals = profile.LearningGoals.Count > 0 || !string.IsNullOrWhiteSpace(profile.CustomLearningGoal);
            var hasFocusAreas = profile.FocusAreas.Count > 0 || !string.IsNullOrWhiteSpace(profile.CustomFocusArea);
            var hasSupportLanguageAnswer = profile.SupportLanguageCode is not null || profile.TranslationHelpPreference is not null;
            var hasAllRequiredPreferences = hasLearningGoals && hasFocusAreas && hasSupportLanguageAnswer;

            if (profile.OnboardingStatus == OnboardingStatus.Complete && hasAllRequiredPreferences)
            {
                // Existing v1-complete student who already has the newer preference fields:
                // initialise v2 progress as already done.
                progress = StudentOnboardingProgress.CreateCompleted(query.UserId, flow.Id);
            }
            else if (profile.OnboardingStatus == OnboardingStatus.Complete)
            {
                // v1-complete but missing one or more required v2 preference fields: route
                // straight to the first missing preference step rather than restarting
                // onboarding from scratch. Falls back to the flow's first enabled step if none
                // of the three known preference step keys are present in this flow definition.
                var missingStepKey = new[]
                    {
                        (!hasSupportLanguageAnswer, "support_language"),
                        (!hasLearningGoals, "learning_goals"),
                        (!hasFocusAreas, "focus_areas"),
                    }
                    .Where(x => x.Item1)
                    .Select(x => x.Item2)
                    .FirstOrDefault(key => flow.Steps.Any(s => s.IsEnabled && s.StepKey == key))
                    ?? flow.Steps.Where(s => s.IsEnabled).OrderBy(s => s.StepOrder).FirstOrDefault()?.StepKey;

                progress = new StudentOnboardingProgress(query.UserId, flow.Id, missingStepKey);
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
