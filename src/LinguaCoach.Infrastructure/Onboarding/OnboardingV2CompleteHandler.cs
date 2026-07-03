using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Services;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingV2CompleteHandler : IOnboardingV2CompleteHandler
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingV2CompleteHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<CompleteOnboardingV2Result> HandleAsync(CompleteOnboardingV2Command command, CancellationToken ct = default)
    {
        var progress = await _db.StudentOnboardingProgress
            .FirstOrDefaultAsync(p => p.UserId == command.UserId, ct)
            ?? throw new OnboardingV2ValidationException("No onboarding progress found.");

        if (progress.IsComplete)
            throw new OnboardingV2ValidationException("Onboarding is already complete.");

        var flow = await _db.OnboardingFlowDefinitions
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.Id == progress.FlowDefinitionId, ct)
            ?? throw new InvalidOperationException("Active onboarding flow not found.");

        // Validate: all SystemRequired + IsEnabled steps must be completed.
        var requiredStepKeys = flow.Steps
            .Where(s => s.RequirementType == OnboardingStepRequirementType.SystemRequired && s.IsEnabled)
            .Select(s => s.StepKey)
            .ToList();

        var missing = requiredStepKeys
            .Where(k => !progress.CompletedStepKeys.Contains(k))
            .ToList();

        if (missing.Any())
            throw new OnboardingV2ValidationException(
                $"Required steps not completed: {string.Join(", ", missing)}.");

        // Compute preliminary CEFR from assessment responses.
        var assessmentSteps = flow.Steps
            .Where(s => s.StepType == OnboardingStepTypeV2.AssessmentQuestion && s.IsEnabled && s.AssessmentMetadataJson is not null)
            .ToList();

        var scores = new List<AssessmentScore>();
        if (assessmentSteps.Any())
        {
            var responses = await _db.StudentOnboardingResponses
                .Where(r => r.ProgressId == progress.Id)
                .ToListAsync(ct);

            foreach (var assessStep in assessmentSteps)
            {
                var meta = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(assessStep.AssessmentMetadataJson!);
                if (meta is null) continue;

                var correctKey = meta.TryGetValue("correctAnswerKey", out var ck) ? ck.GetString() : null;
                var weight = meta.TryGetValue("cefrScoreWeight", out var w) && w.ValueKind == JsonValueKind.Number
                    ? w.GetInt32() : 1;

                var response = responses.FirstOrDefault(r => r.StepKey == assessStep.StepKey);
                if (response is null)
                {
                    scores.Add(new AssessmentScore(IsCorrect: false, Weight: weight));
                    continue;
                }

                var answer = JsonSerializer.Deserialize<JsonElement>(response.AnswerJson);
                var selectedKey = answer.TryGetProperty("key", out var sk) ? sk.GetString() : null;
                scores.Add(new AssessmentScore(IsCorrect: selectedKey == correctKey, Weight: weight));
            }
        }

        var preliminaryCefr = PreliminaryCefrCalculator.Calculate(scores);

        // Complete progress.
        progress.Complete(preliminaryCefr);

        // Update StudentProfile: store preliminary CEFR only if no real placement result exists.
        // PreliminaryCefrLevel is stored on progress; StudentProfile.CefrLevel is only touched
        // when it is currently null (i.e. real placement has not run yet).
        var profile = await _db.StudentProfiles.FirstOrDefaultAsync(sp => sp.UserId == command.UserId, ct);
        if (profile is not null)
        {
            if (profile.CefrLevel is null && preliminaryCefr is not null)
                profile.SetCefrLevel(preliminaryCefr);

            // Advance lifecycle: OnboardingRequired/OnboardingInProgress → PlacementRequired.
            if (profile.LifecycleStage is StudentLifecycleStage.OnboardingRequired
                or StudentLifecycleStage.OnboardingInProgress)
            {
                profile.SetLifecycleStage(StudentLifecycleStage.PlacementRequired);
            }

            // Every other handler in the system (activity generation, dashboard, progress,
            // speaking, readiness pool jobs) still gates on the legacy OnboardingStatus field —
            // V2 completion must set it too, or a V2-onboarded student is silently blocked
            // everywhere else in the app.
            profile.MarkOnboardingComplete();
        }

        await _db.SaveChangesAsync(ct);

        return new CompleteOnboardingV2Result(Success: true, PreliminaryCefrLevel: preliminaryCefr);
    }
}
