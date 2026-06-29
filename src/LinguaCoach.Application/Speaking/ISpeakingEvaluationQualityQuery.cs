namespace LinguaCoach.Application.Speaking;

public interface ISpeakingEvaluationQualityQuery
{
    Task<SpeakingEvaluationQualitySummaryDto> GetQualitySummaryAsync(CancellationToken ct = default);
}
