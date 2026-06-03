using LinguaCoach.Application.LearningPath;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Onboarding;

public sealed class OnboardingHandler : IOnboardingHandler, IOnboardingStatusQuery
{
    private readonly LinguaCoachDbContext _db;
    private readonly ILearningPathGenerator _pathGenerator;
    private readonly ILogger<OnboardingHandler> _logger;

    public OnboardingHandler(
        LinguaCoachDbContext db,
        ILearningPathGenerator pathGenerator,
        ILogger<OnboardingHandler> logger)
    {
        _db = db;
        _pathGenerator = pathGenerator;
        _logger = logger;
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
            case SetTrackRequest r:
            {
                var track = await _db.LearningTracks
                    .FirstOrDefaultAsync(t => t.Id == r.LearningTrackId, ct)
                    ?? throw new InvalidOperationException("Learning track not found.");
                profile.SetLearningTrack(track);
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
            case SetSkillRequest r:
                profile.SetSkillFocus(r.SkillFocus);
                break;
            default:
                throw new InvalidOperationException($"Unknown onboarding step type: {request.GetType().Name}");
        }

        await _db.SaveChangesAsync(ct);

        // When onboarding completes (after step 4 / SetSkill), kick off path generation.
        // Fire-and-forget: student proceeds to dashboard immediately; path is ready on first activity request.
        if (profile.OnboardingStatus == OnboardingStatus.Complete)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await _pathGenerator.GenerateAsync(new GenerateLearningPathCommand(request.UserId));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Background path generation failed for user {UserId}. Will retry lazily on first activity request.",
                        request.UserId);
                }
            }, CancellationToken.None);
        }

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
