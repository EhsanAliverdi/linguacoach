namespace LinguaCoach.Application.Curriculum;

/// <summary>
/// Routes a generation request to suitable curriculum objectives and CEFR bands.
/// Pure application-layer service — does not call AI or read student answers directly.
///
/// Routing rules:
///   - Normalize plus-level strings (B2+) to core band (B2) for routing only.
///   - Prefer exact-level objectives.
///   - Never silently lower CEFR level: lower-level content requires AllowReviewOrScaffold=true
///     and produces RoutingReason review/scaffold/remediation/fallback.
///   - Never default context to workplace when learner context is not workplace.
///   - DifficultyPreference influences DifficultyBand selection within the same CEFR level.
/// </summary>
public interface ICurriculumRoutingService
{
    /// <summary>
    /// Returns a routing recommendation for the given request.
    /// Never throws. If no suitable objective is found, returns a safe fallback recommendation.
    /// </summary>
    Task<CurriculumRoutingRecommendation> RecommendAsync(
        CurriculumRoutingRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Normalizes a raw CEFR string to a core band (A1/A2/B1/B2/C1/C2).
    /// B2+ → B2, unknown/null → A1 (safe conservative fallback).
    /// Does not modify StudentProfile.CefrLevel.
    /// </summary>
    string NormalizeCefrLevel(string? rawLevel);
}
