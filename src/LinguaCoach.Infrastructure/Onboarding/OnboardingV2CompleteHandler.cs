using System.Text.Json;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Application.Questions;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Questions;
using LinguaCoach.Domain.Services;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingV2CompleteHandler : IOnboardingV2CompleteHandler
{
    private readonly LinguaCoachDbContext _db;
    private readonly IQuestionScorer _scorer;

    public OnboardingV2CompleteHandler(LinguaCoachDbContext db, IQuestionScorer scorer)
    {
        _db = db;
        _scorer = scorer;
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

        // Unified Question-Schema Phase 6b: a CEFR-scored onboarding question is just a
        // SingleChoice step whose Content has a CorrectAnswerKey set — scored via the same
        // IQuestionScorer placement uses, no separate "AssessmentQuestion" step type needed.
        var scoredSteps = flow.Steps
            .Where(s => s.IsEnabled && s.Content is SingleChoiceQuestion { CorrectAnswerKey: not null })
            .ToList();

        var scores = new List<AssessmentScore>();
        if (scoredSteps.Count > 0)
        {
            var responses = await _db.StudentOnboardingResponses
                .Where(r => r.ProgressId == progress.Id)
                .ToListAsync(ct);

            foreach (var step in scoredSteps)
            {
                var content = (SingleChoiceQuestion)step.Content!;
                var response = responses.FirstOrDefault(r => r.StepKey == step.StepKey);
                if (response is null)
                {
                    scores.Add(new AssessmentScore(IsCorrect: false, Weight: 1));
                    continue;
                }

                var answer = ParseAnswer(response.AnswerJson);
                var scoreResult = _scorer.Score(content, answer);
                scores.Add(new AssessmentScore(IsCorrect: scoreResult.IsCorrect, Weight: 1));
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

    private static QuestionAnswer ParseAnswer(string answerJson) =>
        QuestionContentJson.TryDeserializeAnswerOrEmpty(answerJson);
}
