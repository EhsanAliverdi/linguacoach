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
 *   Complete   ──any step───────► DomainException (profile is immutable once complete)
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

    // Set after CEFR assessment (T10). Null until assessment is taken.
    public string? CefrLevel { get; private set; }

    // ── Lifecycle & placement sprint fields (T29) ────────────────────────────
    public StudentLifecycleStage LifecycleStage { get; private set; }
    public ProfessionalExperienceLevel? ProfessionalExperienceLevel { get; private set; }
    public RoleFamiliarity? RoleFamiliarity { get; private set; }
    public DomainComplexity? WorkplaceSeniority { get; private set; }
    public int? PreferredSessionDurationMinutes { get; private set; }

    // ── Admin-created profile fields (T30) ──────────────────────────────────
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? DisplayName { get; private set; }
    public string? CareerContext { get; private set; }
    public string? LearningGoal { get; private set; }

    // ── Student-set onboarding goal fields (T31) ────────────────────────────
    public string? LearningGoalDescription { get; private set; }
    public string? DifficultSituationsText { get; private set; }

    private StudentProfile() { }

    public StudentProfile(Guid userId)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId must not be empty.", nameof(userId));

        UserId = userId;
        OnboardingStatus = OnboardingStatus.NotStarted;
        LastCompletedStep = OnboardingStep.None;
        LifecycleStage = StudentLifecycleStage.Created;
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

    public void SetSkillFocus(SkillFocus skillFocus)
    {
        EnsureStepIsNext(OnboardingStep.Skill);

        SkillFocus = skillFocus;
        AdvanceTo(OnboardingStep.Skill);
        OnboardingStatus = OnboardingStatus.Complete;
    }

    // Free-text career path: does not require a CareerProfile FK.
    public void SetCareerContextText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Career context text is required.", nameof(text));
        EnsureStepIsNext(OnboardingStep.Career);

        CareerContext = text.Trim();
        // CareerProfileId intentionally left null for free-text path.
        AdvanceTo(OnboardingStep.Career);
    }

    // Skill step that also captures student-authored learning goals (any language).
    public void SetSkillAndGoal(SkillFocus skillFocus, string? learningGoalDescription, string? difficultSituationsText)
    {
        EnsureStepIsNext(OnboardingStep.Skill);

        SkillFocus = skillFocus;
        LearningGoalDescription = string.IsNullOrWhiteSpace(learningGoalDescription) ? null : learningGoalDescription.Trim();
        DifficultSituationsText = string.IsNullOrWhiteSpace(difficultSituationsText) ? null : difficultSituationsText.Trim();
        AdvanceTo(OnboardingStep.Skill);
        OnboardingStatus = OnboardingStatus.Complete;
    }

    public void SetCefrLevel(string level)
    {
        if (string.IsNullOrWhiteSpace(level)) throw new ArgumentException("CEFR level is required.", nameof(level));
        var valid = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        if (!valid.Contains(level.ToUpperInvariant()))
            throw new ArgumentException($"Invalid CEFR level '{level}'. Must be one of: A1, A2, B1, B2, C1, C2.", nameof(level));
        CefrLevel = level.ToUpperInvariant();
    }

    // ── Lifecycle stage ─────────────────────────────────────────────────────

    public void SetLifecycleStage(StudentLifecycleStage stage)
    {
        LifecycleStage = stage;
    }

    // ── Admin-set initial profile (T30) ─────────────────────────────────────
    // Called once when an admin creates a student with optional profile context.
    // Does NOT advance onboarding steps — those still belong to the student.
    public void SetInitialProfile(
        string? firstName,
        string? lastName,
        string? displayName,
        string? careerContext,
        string? learningGoal,
        int? preferredSessionDurationMinutes,
        ProfessionalExperienceLevel? experienceLevel,
        RoleFamiliarity? roleFamiliarity)
    {
        FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        CareerContext = string.IsNullOrWhiteSpace(careerContext) ? null : careerContext.Trim();
        LearningGoal = string.IsNullOrWhiteSpace(learningGoal) ? null : learningGoal.Trim();

        if (preferredSessionDurationMinutes.HasValue && preferredSessionDurationMinutes.Value > 0)
            PreferredSessionDurationMinutes = preferredSessionDurationMinutes;

        if (experienceLevel.HasValue && roleFamiliarity.HasValue)
        {
            ProfessionalExperienceLevel = experienceLevel;
            RoleFamiliarity = roleFamiliarity;
            WorkplaceSeniority = WorkplaceSeniorityCalculator.Compute(experienceLevel.Value, roleFamiliarity.Value);
        }
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void EnsureStepIsNext(OnboardingStep requestedStep)
    {
        // Once complete, the profile is immutable — no step can overwrite data.
        if (OnboardingStatus == OnboardingStatus.Complete)
            throw new DomainException("Onboarding is already complete and cannot be modified.");

        // Steps must advance in order. Backward re-application would corrupt
        // cross-pair consistency (e.g. LanguagePairId diverging from LearningTrack).
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
