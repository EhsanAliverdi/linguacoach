namespace LinguaCoach.Application.UsageGovernance;

public sealed class UsageSummary
{
    public Guid StudentProfileId { get; init; }
    public string Period { get; init; } = string.Empty;
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }

    public int TotalTokens { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal TotalCost { get; init; }
    public int AiCallCount { get; init; }
    public int LessonGenerations { get; init; }
    public int PracticeGenerations { get; init; }
    public int WritingEvaluations { get; init; }
    public int SpeakingEvaluations { get; init; }
    public int PreparedActivitiesCompleted { get; init; }
}
