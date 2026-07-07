using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Onboarding;

// ── Step request discriminated union ─────────────────────────────────────────

public abstract record OnboardingStepRequest(Guid UserId);

public sealed record SetLanguageRequest(Guid UserId, Guid LanguagePairId)
    : OnboardingStepRequest(UserId);

// Session preference step: student sets their preferred lesson duration.
public sealed record SetSessionPreferenceRequest(Guid UserId, int PreferredDurationMinutes)
    : OnboardingStepRequest(UserId);

public sealed record SetCareerRequest(Guid UserId, Guid CareerProfileId)
    : OnboardingStepRequest(UserId);

// Free-text career path — no CareerProfileId required.
public sealed record SetCareerContextTextRequest(Guid UserId, string CareerContext)
    : OnboardingStepRequest(UserId);

public sealed record SetSkillRequest(Guid UserId, SkillFocus SkillFocus)
    : OnboardingStepRequest(UserId);

// Skill step with optional student-authored learning goal (any language accepted).
public sealed record SetSkillGoalRequest(
    Guid UserId,
    SkillFocus SkillFocus,
    string? LearningGoalDescription,
    string? DifficultSituationsText)
    : OnboardingStepRequest(UserId);

// ── Experience enrichment (step 5 — bypasses state machine ordering) ─────────

public sealed record SetExperienceRequest(
    Guid UserId,
    ProfessionalExperienceLevel ProfessionalExperienceLevel,
    RoleFamiliarity RoleFamiliarity);

public sealed record SetExperienceResult(bool Success);

public interface IOnboardingExperienceHandler
{
    Task<SetExperienceResult> HandleAsync(SetExperienceRequest request, CancellationToken ct = default);
}

// ── Handler interface ─────────────────────────────────────────────────────────

public sealed record OnboardingStepResult(string LastCompletedStep, bool IsComplete);

public interface IOnboardingHandler
{
    Task<OnboardingStepResult> HandleAsync(OnboardingStepRequest request, CancellationToken ct = default);
}

// ── Status query ──────────────────────────────────────────────────────────────

public sealed record OnboardingStatusQuery(Guid UserId);

public sealed record OnboardingStatusResult(string CurrentStep, bool IsComplete, Guid? LanguagePairId);

public interface IOnboardingStatusQuery
{
    Task<OnboardingStatusResult> HandleAsync(OnboardingStatusQuery query, CancellationToken ct = default);
}
