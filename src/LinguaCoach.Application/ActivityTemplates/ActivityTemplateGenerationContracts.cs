namespace LinguaCoach.Application.ActivityTemplates;

/// <summary>
/// Phase 5 of the AI bank-first teaching architecture
/// (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md): AI personalizes an
/// instance from an approved <c>ActivityTemplate</c>, validated against the template's own
/// <c>ValidationRulesJson</c> and the shared Form.io student-safe schema rules before it is
/// ever surfaced. This phase proves the pipeline (admin "generate preview" only) — persisting a
/// generated instance into a per-student pool is Phase 6.
/// </summary>
public sealed record ActivityTemplateInstanceGenerationContext(
    string? CefrLevelOverride = null,
    string? TopicHint = null,
    string? LearnerPreferenceContext = null,
    Guid? StudentProfileId = null,
    string GenerationSource = "AdminPreview");

public sealed record ActivityTemplateInstanceResult(
    Guid TemplateId,
    string GeneratedSchemaJson,
    string ProviderName,
    string ModelName,
    string? CorrelationId);

public interface IActivityTemplateInstanceGenerator
{
    /// <summary>
    /// Generates a personalized instance of the given template's Form.io base schema via AI,
    /// validates it, and returns the result. Never persists anything.
    /// Throws <see cref="ActivityTemplateValidationException"/> if the template is missing
    /// required authoring fields, <see cref="LinguaCoach.Application.Ai.AiResponseValidationException"/>
    /// if AI output fails validation after one retry, or
    /// <see cref="LinguaCoach.Application.Ai.AiUnavailableException"/> if the AI provider is unreachable.
    /// </summary>
    Task<ActivityTemplateInstanceResult> GenerateInstanceAsync(
        Guid templateId,
        ActivityTemplateInstanceGenerationContext context,
        CancellationToken ct = default);
}
