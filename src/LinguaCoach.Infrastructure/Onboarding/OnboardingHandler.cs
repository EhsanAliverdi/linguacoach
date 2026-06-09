using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingHandler : IOnboardingHandler, IOnboardingStatusQuery
{
    private readonly LinguaCoachDbContext _db;

    public OnboardingHandler(LinguaCoachDbContext db)
    {
        _db = db;
    }

    public async Task<OnboardingStepResult> HandleAsync(OnboardingStepRequest request, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == request.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        switch (request)
        {
            case SetLanguageRequest r:
            {
                var pair = await _db.LanguagePairs
                    .Include(lp => lp.SourceLanguage)
                    .Include(lp => lp.TargetLanguage)
                    .FirstOrDefaultAsync(lp => lp.Id == r.LanguagePairId, ct)
                    ?? throw new InvalidOperationException("Language pair not found.");
                profile.SetLanguagePair(pair);
                break;
            }
            case SetSessionPreferenceRequest r:
                profile.SetSessionPreference(r.PreferredDurationMinutes);
                break;
#pragma warning disable CS0612
            case SetTrackRequest r:
            {
                var track = await _db.LearningTracks
                    .FirstOrDefaultAsync(t => t.Id == r.LearningTrackId, ct)
                    ?? throw new InvalidOperationException("Learning track not found.");
#pragma warning disable CS0618
                profile.SetLearningTrack(track);
#pragma warning restore CS0618
#pragma warning restore CS0612
                break;
            }
            case SetCareerRequest r:
            {
                var career = await _db.CareerProfiles
                    .FirstOrDefaultAsync(c => c.Id == r.CareerProfileId, ct)
                    ?? throw new InvalidOperationException("Career profile not found.");
                profile.SetCareerProfile(career);
                break;
            }
            case SetCareerContextTextRequest r:
                profile.SetCareerContextText(r.CareerContext);
                break;
            case SetSkillGoalRequest r:
                profile.SetSkillAndGoal(r.SkillFocus, r.LearningGoalDescription, r.DifficultSituationsText);
                break;
            case SetSkillRequest r:
                profile.SetSkillFocus(r.SkillFocus);
                break;
            default:
                throw new InvalidOperationException($"Unknown onboarding step type: {request.GetType().Name}");
        }

        if (profile.OnboardingStatus == OnboardingStatus.Complete)
            profile.SetLifecycleStage(LinguaCoach.Domain.Enums.StudentLifecycleStage.PlacementRequired);

        await _db.SaveChangesAsync(ct);

        return new OnboardingStepResult(profile.LastCompletedStep.ToString(), profile.OnboardingStatus == OnboardingStatus.Complete);
    }

    public async Task<OnboardingStatusResult> HandleAsync(OnboardingStatusQuery query, CancellationToken ct = default)
    {
        var profile = await _db.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == query.UserId, ct)
            ?? throw new InvalidOperationException("Student profile not found.");

        return new OnboardingStatusResult(
            profile.LastCompletedStep.ToString(),
            profile.OnboardingStatus == OnboardingStatus.Complete,
            profile.LanguagePairId);
    }
}
