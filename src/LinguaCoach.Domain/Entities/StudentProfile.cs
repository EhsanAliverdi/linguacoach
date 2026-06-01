using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Exceptions;

namespace LinguaCoach.Domain.Entities;

/*
 * Onboarding state machine:
 *
 *   NotStarted ──step: Language──► InProgress (LastCompletedStep = None → Language)
 *   InProgress ──step: Track────► InProgress (LastCompletedStep = Language → Track)
 *   InProgress ──step: Career───► InProgress (LastCompletedStep = Track → Career)
 *   InProgress ──step: Skill────► Complete   (LastCompletedStep = Career → Skill)
 *   Complete   ──any step───────► Complete   (idempotent, no state change)
 *
 *   Steps must be completed in order. Attempting step N+2 before step N+1 raises
 *   OnboardingStepOutOfOrderException.
 */
public sealed class StudentProfile : BaseEntity
{
    /// <summary>
    /// Identity user ID (from ASP.NET Identity). Not a navigation property here —
    /// Domain does not depend on Identity.
    /// </summary>
    public Guid UserId { get; private set; }

    public OnboardingStatus OnboardingStatus { get; private set; }
    public OnboardingStep LastCompletedStep { get; private set; }

    // Set during onboarding — nullable until each step is complete.
    public Guid? LanguagePairId { get; private set; }
    public LanguagePair? LanguagePair { get; private set; }

    public Guid? LearningTrackId { get; private set; }
    public LearningTrack? LearningTrack { get; private set; }

    public Guid? CareerProfileId { get; private set; }
    public CareerProfile? CareerProfile { get; private set; }

    public SkillFocus? SkillFocus { get; private set; }

    private StudentProfile() { }

    public StudentProfile(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId must not be empty.", nameof(userId));

        UserId = userId;
        OnboardingStatus = OnboardingStatus.NotStarted;
        LastCompletedStep = OnboardingStep.None;
    }

    // ── Onboarding step methods ─────────────────────────────────────────────

    public void SetLanguagePair(LanguagePair languagePair)
    {
        ArgumentNullException.ThrowIfNull(languagePair);
        EnsureStepIsNext(OnboardingStep.Language);

        LanguagePairId = languagePair.Id;
        LanguagePair = languagePair;
        AdvanceTo(OnboardingStep.Language);
    }

    public void SetLearningTrack(LearningTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);
        EnsureStepIsNext(OnboardingStep.Track);

        if (LanguagePairId is null || track.LanguagePairId != LanguagePairId)
            throw new DomainException("Learning track must belong to the student's selected language pair.");

        LearningTrackId = track.Id;
        LearningTrack = track;
        AdvanceTo(OnboardingStep.Track);
    }

    public void SetCareerProfile(CareerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        EnsureStepIsNext(OnboardingStep.Career);

        if (LanguagePairId is null || profile.LanguagePairId != LanguagePairId)
            throw new DomainException("Career profile must belong to the student's selected language pair.");

        CareerProfileId = profile.Id;
        CareerProfile = profile;
        AdvanceTo(OnboardingStep.Career);
    }

    public void SetSkillFocus(Enums.SkillFocus skillFocus)
    {
        EnsureStepIsNext(OnboardingStep.Skill);

        SkillFocus = skillFocus;
        AdvanceTo(OnboardingStep.Skill);
        OnboardingStatus = OnboardingStatus.Complete;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void EnsureStepIsNext(OnboardingStep requestedStep)
    {
        // Idempotent: already complete, silently allow re-application.
        if (OnboardingStatus == OnboardingStatus.Complete) return;

        var expectedNext = (OnboardingStep)((int)LastCompletedStep + 1);
        if (requestedStep > expectedNext)
            throw new OnboardingStepOutOfOrderException(requestedStep, expectedNext);
    }

    private void AdvanceTo(OnboardingStep step)
    {
        LastCompletedStep = step;
        if (OnboardingStatus == OnboardingStatus.NotStarted)
            OnboardingStatus = OnboardingStatus.InProgress;
    }
}
