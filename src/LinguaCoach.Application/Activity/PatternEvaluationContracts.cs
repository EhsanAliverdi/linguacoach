using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

public sealed record PatternEvaluationRequest(
    Guid ActivityId,
    Guid StudentProfileId,
    string? ExercisePatternKey,
    MarkingMode MarkingMode,
    InteractionMode? InteractionMode,
    ActivityType ActivityType,
    string ContentJson,
    string SubmittedAnswerJson,
    string? CefrLevel = null,
    string? DomainComplexity = null,
    string? SessionContextJson = null,
    string? StudentSkillContext = null,
    string? SourceLanguageName = null,
    string? TargetLanguageName = null);

public sealed class PatternEvaluationResult
{
    public double Score { get; init; }
    public double MaxScore { get; init; }
    public double Percentage { get; init; }
    public bool Passed { get; init; }
    public bool Completed { get; init; }
    public IReadOnlyList<PatternEvaluationItemResult> ItemResults { get; init; } = [];
    public string? CoachSummary { get; init; }
    public IReadOnlyList<PatternEvaluationCorrection> Corrections { get; init; } = [];
    public string? SuggestedImprovedAnswer { get; init; }
    public IReadOnlyList<PatternEvaluationSkillImpact> SkillImpacts { get; init; } = [];
    public IReadOnlyList<PatternEvaluationMemorySignal> MemorySignals { get; init; } = [];

    public static PatternEvaluationResult Create(
        double score,
        double maxScore,
        bool passed,
        bool completed,
        IReadOnlyList<PatternEvaluationItemResult>? itemResults = null,
        string? coachSummary = null,
        IReadOnlyList<PatternEvaluationCorrection>? corrections = null,
        string? suggestedImprovedAnswer = null,
        IReadOnlyList<PatternEvaluationSkillImpact>? skillImpacts = null,
        IReadOnlyList<PatternEvaluationMemorySignal>? memorySignals = null)
    {
        if (score < 0) throw new ArgumentOutOfRangeException(nameof(score), "Score must not be negative.");
        if (maxScore < 0) throw new ArgumentOutOfRangeException(nameof(maxScore), "MaxScore must not be negative.");
        if (maxScore > 0 && score > maxScore)
            throw new ArgumentOutOfRangeException(nameof(score), "Score must not exceed MaxScore.");

        return new PatternEvaluationResult
        {
            Score = score,
            MaxScore = maxScore,
            Percentage = CalculatePercentage(score, maxScore),
            Passed = passed,
            Completed = completed,
            ItemResults = itemResults ?? [],
            CoachSummary = string.IsNullOrWhiteSpace(coachSummary) ? null : coachSummary.Trim(),
            Corrections = corrections ?? [],
            SuggestedImprovedAnswer = string.IsNullOrWhiteSpace(suggestedImprovedAnswer) ? null : suggestedImprovedAnswer.Trim(),
            SkillImpacts = skillImpacts ?? [],
            MemorySignals = memorySignals ?? []
        };
    }

    public static double CalculatePercentage(double score, double maxScore)
    {
        if (score < 0) throw new ArgumentOutOfRangeException(nameof(score), "Score must not be negative.");
        if (maxScore < 0) throw new ArgumentOutOfRangeException(nameof(maxScore), "MaxScore must not be negative.");
        if (maxScore == 0) return 0;

        return Math.Round(score / maxScore * 100, 2, MidpointRounding.AwayFromZero);
    }
}

public sealed record PatternEvaluationItemResult(
    string ItemKey,
    string? StudentAnswer,
    string? CorrectAnswer,
    IReadOnlyList<string> AcceptedAnswers,
    bool IsCorrect,
    double Score,
    double MaxScore,
    string? Feedback);

public sealed record PatternEvaluationCorrection(
    string Category,
    string? Original,
    string Suggestion,
    string Explanation);

public sealed record PatternEvaluationSkillImpact(
    string SkillKey,
    string Label,
    double Delta,
    string? Evidence);

public sealed record PatternEvaluationMemorySignal(
    string Type,
    string Key,
    string Summary,
    double Confidence);
