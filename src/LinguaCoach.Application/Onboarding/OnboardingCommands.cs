using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Onboarding;

// ── Step request discriminated union ─────────────────────────────────────────

public abstract record OnboardingStepRequest(Guid UserId);

public sealed record SetLanguageRequest(Guid UserId, Guid LanguagePairId)
    : OnboardingStepRequest(UserId);

public sealed record SetTrackRequest(Guid UserId, Guid LearningTrackId)
    : OnboardingStepRequest(UserId);

public sealed record SetCareerRequest(Guid UserId, Guid CareerProfileId)
    : OnboardingStepRequest(UserId);

public sealed record SetSkillRequest(Guid UserId, SkillFocus SkillFocus)
    : OnboardingStepRequest(UserId);

// ── Handler interface ─────────────────────────────────────────────────────────

public sealed record OnboardingStepResult(string LastCompletedStep, bool IsComplete);

public interface IOnboardingHandler
{
    Task<OnboardingStepResult> HandleAsync(OnboardingStepRequest request, CancellationToken ct = default);
}

// ── Status query ──────────────────────────────────────────────────────────────

public sealed record OnboardingStatusQuery(Guid UserId);

public sealed record OnboardingStatusResult(string CurrentStep, bool IsComplete);

public interface IOnboardingStatusQuery
{
    Task<OnboardingStatusResult> HandleAsync(OnboardingStatusQuery query, CancellationToken ct = default);
}
