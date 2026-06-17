namespace LinguaCoach.Application.UsageGovernance;

public sealed class QuotaExceededException : Exception
{
    public QuotaDecision Decision { get; }

    public QuotaExceededException(QuotaDecision decision)
        : base(decision.Reason ?? $"Quota exceeded for feature '{decision.FeatureKey}'.")
    {
        Decision = decision;
    }
}
