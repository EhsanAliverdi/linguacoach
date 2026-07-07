using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;
using LinguaCoach.Domain.Exceptions;

namespace LinguaCoach.Domain.Entities;

/*
 * Onboarding state machine:
 *
 *   NotStarted ──step: Language────► InProgress (LastCompletedStep = None → Language)
 *   InProgress ──step: Preference──► InProgress (LastCompletedStep = Language → Preference)
 *   InProgress ──step: Career──────► InProgress (LastCompletedStep = Preference → Career)
 *   InProgress ──step: Skill───────► Complete   (LastCompletedStep = Career → Skill)
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

    // ── Student-editable learning preferences (T46 / Phase 10G) ─────────────
    public string? PreferredName { get; private set; }
    public string? SupportLanguageCode { get; private set; }
    public string? SupportLanguageName { get; private set; }
    public TranslationHelpPreference? TranslationHelpPreference { get; private set; }
    public List<string> LearningGoals { get; private set; } = new();
    public string? CustomLearningGoal { get; private set; }
    public List<string> FocusAreas { get; private set; } = new();
    public string? CustomFocusArea { get; private set; }
    public DifficultyPreference? DifficultyPreference { get; private set; }
    public DateTimeOffset? LearningPreferencesUpdatedAt { get; private set; }

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

    // Session preference step: student picks their preferred lesson duration.
    public void SetSessionPreference(int preferredDurationMinutes)
    {
        if (preferredDurationMinutes <= 0)
            throw new ArgumentException("Preferred session duration must be positive.", nameof(preferredDurationMinutes));
        EnsureStepIsNext(OnboardingStep.Preference);

        PreferredSessionDurationMinutes = preferredDurationMinutes;
        AdvanceTo(OnboardingStep.Preference);
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

    /// <summary>
    /// Marks onboarding complete without driving the legacy V1 step state machine. Called by
    /// onboarding V2's completion handler — every other handler in the system (activity
    /// generation, dashboard, progress, speaking, readiness pool jobs) still gates on this
    /// legacy field, so V2 completion must set it too.
    /// </summary>
    public void MarkOnboardingComplete()
    {
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

    public void UpdateAdminProfile(
        string? firstName,
        string? lastName,
        string? displayName,
        string? careerContext,
        string? learningGoal,
        string? learningGoalDescription,
        string? difficultSituationsText,
        int? preferredSessionDurationMinutes,
        ProfessionalExperienceLevel? experienceLevel,
        RoleFamiliarity? roleFamiliarity)
    {
        FirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
        LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
        CareerContext = string.IsNullOrWhiteSpace(careerContext) ? null : careerContext.Trim();
        LearningGoal = string.IsNullOrWhiteSpace(learningGoal) ? null : learningGoal.Trim();
        LearningGoalDescription = string.IsNullOrWhiteSpace(learningGoalDescription) ? null : learningGoalDescription.Trim();
        DifficultSituationsText = string.IsNullOrWhiteSpace(difficultSituationsText) ? null : difficultSituationsText.Trim();

        PreferredSessionDurationMinutes =
            preferredSessionDurationMinutes.HasValue && preferredSessionDurationMinutes.Value > 0
                ? preferredSessionDurationMinutes
                : null;

        ProfessionalExperienceLevel = experienceLevel;
        RoleFamiliarity = roleFamiliarity;
        WorkplaceSeniority = experienceLevel.HasValue && roleFamiliarity.HasValue
            ? WorkplaceSeniorityCalculator.Compute(experienceLevel.Value, roleFamiliarity.Value)
            : null;
    }

    /// <summary>
    /// Sets professional experience and role familiarity without touching any other profile fields.
    /// Intentionally bypasses the onboarding state machine — safe to call after onboarding is complete.
    /// Computes and persists WorkplaceSeniority immediately.
    /// </summary>
    public void SetExperienceContext(
        ProfessionalExperienceLevel experienceLevel,
        RoleFamiliarity roleFamiliarity)
    {
        ProfessionalExperienceLevel = experienceLevel;
        RoleFamiliarity = roleFamiliarity;
        WorkplaceSeniority = WorkplaceSeniorityCalculator.Compute(experienceLevel, roleFamiliarity);
    }

    /// <summary>
    /// Sets just the experience-level half of work experience (Unified Question-Schema Phase 6b —
    /// WorkExperience is now two independent onboarding steps instead of one composite step).
    /// Recomputes WorkplaceSeniority once both halves are known; leaves it untouched otherwise.
    /// </summary>
    public void SetProfessionalExperienceLevel(ProfessionalExperienceLevel experienceLevel)
    {
        ProfessionalExperienceLevel = experienceLevel;
        RecomputeWorkplaceSeniority();
    }

    /// <summary>Sets just the role-familiarity half of work experience. See SetProfessionalExperienceLevel.</summary>
    public void SetRoleFamiliarity(RoleFamiliarity roleFamiliarity)
    {
        RoleFamiliarity = roleFamiliarity;
        RecomputeWorkplaceSeniority();
    }

    private void RecomputeWorkplaceSeniority()
    {
        if (ProfessionalExperienceLevel.HasValue && RoleFamiliarity.HasValue)
            WorkplaceSeniority = WorkplaceSeniorityCalculator.Compute(ProfessionalExperienceLevel.Value, RoleFamiliarity.Value);
    }

    /// <summary>
    /// Admin reset: clears onboarding selections and returns the student to the
    /// start of onboarding. Does not touch admin-set profile fields (name, career
    /// context) or CEFR level — those are cleared separately if requested.
    /// </summary>
    public void ResetToOnboarding()
    {
        OnboardingStatus = OnboardingStatus.NotStarted;
        LastCompletedStep = OnboardingStep.None;
        LanguagePairId = null;
        LanguagePair = null;
        CareerProfileId = null;
        CareerProfile = null;
        SkillFocus = null;
        LearningGoalDescription = null;
        DifficultSituationsText = null;
        LifecycleStage = StudentLifecycleStage.OnboardingRequired;
    }

    // ── Student-editable learning preferences (T46 / Phase 10G) ─────────────

    /// <summary>
    /// Updates only student-editable preference fields.
    /// Never touches CefrLevel, prompts, admin fields, or onboarding state.
    /// </summary>
    public void UpdateLearningPreferences(
        string? preferredName,
        string? supportLanguageCode,
        string? supportLanguageName,
        TranslationHelpPreference? translationHelpPreference,
        IReadOnlyList<string>? learningGoals,
        string? customLearningGoal,
        IReadOnlyList<string>? focusAreas,
        string? customFocusArea,
        DifficultyPreference? difficultyPreference,
        int? preferredSessionDurationMinutes)
    {
        if (preferredName is not null && preferredName.Length > 100)
            throw new ArgumentException("PreferredName must not exceed 100 characters.", nameof(preferredName));

        if (supportLanguageCode is not null && supportLanguageCode.Length > 10)
            throw new ArgumentException("SupportLanguageCode must not exceed 10 characters.", nameof(supportLanguageCode));

        if (supportLanguageName is not null && supportLanguageName.Length > 100)
            throw new ArgumentException("SupportLanguageName must not exceed 100 characters.", nameof(supportLanguageName));

        if (customLearningGoal is not null && customLearningGoal.Length > 200)
            throw new ArgumentException("CustomLearningGoal must not exceed 200 characters.", nameof(customLearningGoal));

        if (customFocusArea is not null && customFocusArea.Length > 200)
            throw new ArgumentException("CustomFocusArea must not exceed 200 characters.", nameof(customFocusArea));

        if (learningGoals is not null)
        {
            if (learningGoals.Count > 10)
                throw new ArgumentException("No more than 10 learning goals are allowed.", nameof(learningGoals));
            if (learningGoals.Any(g => g.Length > 100))
                throw new ArgumentException("Each learning goal must not exceed 100 characters.", nameof(learningGoals));
        }

        if (focusAreas is not null)
        {
            if (focusAreas.Count > 10)
                throw new ArgumentException("No more than 10 focus areas are allowed.", nameof(focusAreas));
            if (focusAreas.Any(f => f.Length > 100))
                throw new ArgumentException("Each focus area must not exceed 100 characters.", nameof(focusAreas));
        }

        if (preferredSessionDurationMinutes.HasValue && preferredSessionDurationMinutes.Value > 0)
            PreferredSessionDurationMinutes = preferredSessionDurationMinutes;

        PreferredName = string.IsNullOrWhiteSpace(preferredName) ? null : preferredName.Trim();
        SupportLanguageCode = string.IsNullOrWhiteSpace(supportLanguageCode) ? null : supportLanguageCode.Trim();
        SupportLanguageName = string.IsNullOrWhiteSpace(supportLanguageName) ? null : supportLanguageName.Trim();
        TranslationHelpPreference = translationHelpPreference;
        LearningGoals = learningGoals is not null ? learningGoals.ToList() : LearningGoals;
        CustomLearningGoal = string.IsNullOrWhiteSpace(customLearningGoal) ? null : customLearningGoal.Trim();
        FocusAreas = focusAreas is not null ? focusAreas.ToList() : FocusAreas;
        CustomFocusArea = string.IsNullOrWhiteSpace(customFocusArea) ? null : customFocusArea.Trim();
        DifficultyPreference = difficultyPreference;
        LearningPreferencesUpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Partial update for free-text onboarding context (career context, why-learning
    /// description). Each parameter only updates its own field when non-null — bypasses the
    /// onboarding state machine, safe to call from onboarding V2's per-step submission model
    /// or after onboarding is complete (e.g. from /profile).
    /// </summary>
    public void UpdateOnboardingFreeTextContext(string? careerContextText, string? learningGoalDescription)
    {
        if (careerContextText is not null)
        {
            if (careerContextText.Length > 500)
                throw new ArgumentException("Career context text must not exceed 500 characters.", nameof(careerContextText));
            CareerContext = string.IsNullOrWhiteSpace(careerContextText) ? null : careerContextText.Trim();
        }

        if (learningGoalDescription is not null)
        {
            if (learningGoalDescription.Length > 1000)
                throw new ArgumentException("Learning goal description must not exceed 1000 characters.", nameof(learningGoalDescription));
            LearningGoalDescription = string.IsNullOrWhiteSpace(learningGoalDescription) ? null : learningGoalDescription.Trim();
        }
    }

    /// <summary>Admin reset: clears placement results (CEFR level).</summary>
    public void ClearPlacementResult()
    {
        CefrLevel = null;
    }

    /// <summary>Admin action: set or clear CEFR level directly. Bypasses assessment flow.</summary>
    public void AdminSetCefrLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            CefrLevel = null;
            return;
        }
        var normalised = level.Trim().ToUpperInvariant();
        var valid = new[] { "A1", "A2", "B1", "B2", "C1", "C2" };
        if (!valid.Contains(normalised))
            throw new ArgumentException($"Invalid CEFR level '{level}'. Must be one of: A1, A2, B1, B2, C1, C2.", nameof(level));
        CefrLevel = normalised;
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void EnsureStepIsNext(OnboardingStep requestedStep)
    {
        // Once complete, the profile is immutable — no step can overwrite data.
        if (OnboardingStatus == OnboardingStatus.Complete)
            throw new DomainException("Onboarding is already complete and cannot be modified.");

        // Steps must advance in order. Backward re-application would corrupt
        // cross-pair consistency (e.g. LanguagePairId diverging from CareerProfile).
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
