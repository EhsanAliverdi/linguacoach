using System.Text.Json;
using System.Text.Json.Serialization;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Placement;
using LinguaCoach.Infrastructure.Ai;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.Placement;

/// <summary>
/// Placement evaluator backed by the <c>placement_assessment_evaluate</c> AI prompt.
/// Sends compact section summaries only (never raw correct answers) and parses a structured
/// PlacementResult. Falls back to the deterministic evaluator if the AI is unavailable or
/// returns unparseable output, so placement always completes.
/// </summary>
public sealed class AiPlacementEvaluator : IPlacementEvaluator
{
    public const string PromptKey = "placement_assessment_evaluate";

    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly FakePlacementEvaluator _fallback;
    private readonly ILogger<AiPlacementEvaluator> _logger;

    public AiPlacementEvaluator(
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        FakePlacementEvaluator fallback,
        ILogger<AiPlacementEvaluator> logger)
    {
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<PlacementEvaluationResult> EvaluateAsync(PlacementEvaluationInput input, CancellationToken ct = default)
    {
        try
        {
            var sectionSummaries = JsonSerializer.Serialize(input.Sections.Select(s => new
            {
                section = s.SectionKey,
                scored = s.Scored,
                score = s.Score,
                answered = s.AnsweredCount,
                correct = s.CorrectCount,
                response = s.ResponseText,
                notes = s.Notes
            }));

            var variables = new Dictionary<string, string>
            {
                ["sourceLanguageName"] = input.SourceLanguageName,
                ["targetLanguageName"] = input.TargetLanguageName,
                ["careerContext"] = input.CareerContext,
                ["professionalExperienceLevel"] = input.ProfessionalExperienceLevel,
                ["roleFamiliarity"] = input.RoleFamiliarity,
                ["domainComplexity"] = input.DomainComplexity,
                ["selfReportedLevel"] = input.SelfReportedLevel ?? "not provided",
                ["sectionSummaries"] = sectionSummaries,
            };

            var aiRequest = await _contextBuilder.BuildAsync(PromptKey, variables, ct);
            var response = await _aiExecution.ExecuteAsync(
                PromptKey, aiRequest, input.StudentProfileId, correlationId: null, ct);

            var parsed = Parse(response);
            if (parsed is not null)
                return parsed;

            _logger.LogWarning("Placement AI returned unparseable result; using deterministic fallback.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Placement AI evaluation failed; using deterministic fallback.");
        }

        return await _fallback.EvaluateAsync(input, ct);
    }

    private static PlacementEvaluationResult? Parse(string raw)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<PlacementResultPayload>(CleanJson(raw),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload is null || string.IsNullOrWhiteSpace(payload.EstimatedOverallLevel))
                return null;

            var duration = payload.RecommendedSessionDuration is 10 or 15 or 20 or 30
                ? payload.RecommendedSessionDuration.Value
                : 15;

            var skills = payload.SkillLevels ?? new Dictionary<string, string>();

            return new PlacementEvaluationResult(
                EstimatedOverallLevel: payload.EstimatedOverallLevel!,
                SkillLevels: skills,
                Strengths: payload.Strengths ?? [],
                Weaknesses: payload.Weaknesses ?? [],
                RecommendedStartingCourse: payload.RecommendedStartingCourse,
                RecommendedSessionDuration: duration,
                PlacementNotes: payload.PlacementNotes);
        }
        catch
        {
            return null;
        }
    }

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (!cleaned.StartsWith("```")) return cleaned;
        var firstNewline = cleaned.IndexOf('\n');
        var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline > 0 && lastFence > firstNewline
            ? cleaned[(firstNewline + 1)..lastFence].Trim()
            : cleaned;
    }

    private sealed class PlacementResultPayload
    {
        [JsonPropertyName("estimatedOverallLevel")] public string? EstimatedOverallLevel { get; set; }
        [JsonPropertyName("skillLevels")] public Dictionary<string, string>? SkillLevels { get; set; }
        [JsonPropertyName("strengths")] public List<string>? Strengths { get; set; }
        [JsonPropertyName("weaknesses")] public List<string>? Weaknesses { get; set; }
        [JsonPropertyName("recommendedStartingCourse")] public string? RecommendedStartingCourse { get; set; }
        [JsonPropertyName("recommendedSessionDuration")] public int? RecommendedSessionDuration { get; set; }
        [JsonPropertyName("placementNotes")] public string? PlacementNotes { get; set; }
    }
}
