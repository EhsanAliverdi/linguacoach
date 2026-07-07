using System.Text.Json;
using LinguaCoach.Application.ActivityTemplates;
using LinguaCoach.Application.Ai;
using LinguaCoach.Application.Onboarding;
using LinguaCoach.Domain.Entities;
using LinguaCoach.Infrastructure.Ai;
using LinguaCoach.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LinguaCoach.Infrastructure.ActivityTemplates;

/// <summary>
/// Phase 5 of the AI bank-first teaching architecture — personalizes an instance from an
/// ActivityTemplate's Form.io base schema via AI, validating the result before returning it.
/// Mirrors the retry-once-then-fail pattern used by AiActivityGeneratorHandler, and reuses
/// IFormIoSchemaValidationService so a template-generated instance is held to exactly the same
/// student-safe schema bar as placement items and onboarding.
/// </summary>
public sealed class ActivityTemplateInstanceGenerator : IActivityTemplateInstanceGenerator
{
    public const string GeneratePromptKey = "activity_template_generate_instance";

    private readonly LinguaCoachDbContext _db;
    private readonly IAiContextBuilder _contextBuilder;
    private readonly AiExecutionService _aiExecution;
    private readonly IFormIoSchemaValidationService _formIoValidator;
    private readonly ILogger<ActivityTemplateInstanceGenerator> _logger;

    public ActivityTemplateInstanceGenerator(
        LinguaCoachDbContext db,
        IAiContextBuilder contextBuilder,
        AiExecutionService aiExecution,
        IFormIoSchemaValidationService formIoValidator,
        ILogger<ActivityTemplateInstanceGenerator> logger)
    {
        _db = db;
        _contextBuilder = contextBuilder;
        _aiExecution = aiExecution;
        _formIoValidator = formIoValidator;
        _logger = logger;
    }

    public async Task<ActivityTemplateInstanceResult> GenerateInstanceAsync(
        Guid templateId,
        ActivityTemplateInstanceGenerationContext context,
        CancellationToken ct = default)
    {
        var template = await _db.ActivityTemplates.FirstOrDefaultAsync(t => t.Id == templateId, ct)
            ?? throw new ActivityTemplateValidationException($"Activity template {templateId} not found.");

        if (string.IsNullOrWhiteSpace(template.FormIoBaseSchemaJson))
            throw new ActivityTemplateValidationException(
                $"Template '{template.Key}' has no FormIoBaseSchemaJson to personalize from.");
        if (string.IsNullOrWhiteSpace(template.GenerationInstructions))
            throw new ActivityTemplateValidationException(
                $"Template '{template.Key}' has no GenerationInstructions — nothing to guide AI personalization.");

        var validationRules = ActivityTemplateValidationRules.Parse(template.ValidationRulesJson);

        var variables = new Dictionary<string, string>
        {
            ["skill"] = template.Skill,
            ["subskill"] = template.Subskill ?? "none",
            ["cefrLevel"] = context.CefrLevelOverride ?? template.CefrLevel,
            ["activityType"] = template.ActivityType,
            ["baseSchema"] = template.FormIoBaseSchemaJson,
            ["generationInstructions"] = template.GenerationInstructions,
            ["topicHint"] = context.TopicHint ?? "everyday real-life communication",
            ["learnerPreferences"] = context.LearnerPreferenceContext ?? string.Empty,
        };

        var aiRequest = await _contextBuilder.BuildAsync(GeneratePromptKey, variables, ct);
        var correlationId = Guid.NewGuid().ToString("N")[..16];

        var result = await _aiExecution.ExecuteWithMetaAsync(
            GeneratePromptKey, aiRequest, context.StudentProfileId, correlationId, ct);

        var cleaned = CleanJson(result.ResponseJson);
        var errors = ValidateCandidate(cleaned, validationRules);

        if (errors.Count > 0)
        {
            await LogValidationFailureAsync(template, context, errors, attemptNumber: 1, correlationId, result.ProviderName, result.ModelName, ct);

            var retryResult = await _aiExecution.ExecuteWithMetaAsync(
                GeneratePromptKey, aiRequest, context.StudentProfileId, correlationId, ct);
            cleaned = CleanJson(retryResult.ResponseJson);
            var retryErrors = ValidateCandidate(cleaned, validationRules);

            if (retryErrors.Count > 0)
            {
                await LogValidationFailureAsync(template, context, retryErrors, attemptNumber: 2, correlationId, retryResult.ProviderName, retryResult.ModelName, ct);
                throw new AiResponseValidationException(
                    $"AI-generated instance for template '{template.Key}' failed validation after retry: {string.Join("; ", retryErrors)}");
            }

            return new ActivityTemplateInstanceResult(template.Id, cleaned, retryResult.ProviderName, retryResult.ModelName, correlationId);
        }

        return new ActivityTemplateInstanceResult(template.Id, cleaned, result.ProviderName, result.ModelName, correlationId);
    }

    private IReadOnlyList<string> ValidateCandidate(string cleanedJson, ActivityTemplateValidationRules rules)
    {
        var errors = new List<string>();

        try
        {
            JsonDocument.Parse(cleanedJson);
        }
        catch (JsonException ex)
        {
            errors.Add($"AI response is not valid JSON: {ex.Message}");
            return errors;
        }

        var schemaResult = _formIoValidator.ValidateSchema(cleanedJson);
        if (!schemaResult.IsValid)
            errors.Add(schemaResult.Error ?? "Generated schema failed Form.io student-safe validation.");

        errors.AddRange(rules.Validate(cleanedJson));

        return errors;
    }

    private static string CleanJson(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.StartsWith("```"))
        {
            var firstNewline = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```");
            if (firstNewline > 0 && lastFence > firstNewline)
                cleaned = cleaned[(firstNewline + 1)..lastFence].Trim();
        }
        return cleaned;
    }

    private async Task LogValidationFailureAsync(
        ActivityTemplate template,
        ActivityTemplateInstanceGenerationContext context,
        IReadOnlyList<string> errors,
        int attemptNumber,
        string? correlationId,
        string? providerName,
        string? modelName,
        CancellationToken ct)
    {
        try
        {
            var failure = new GenerationValidationFailure(
                activityTypeName: template.ActivityType,
                validationErrors: string.Join("; ", errors),
                attemptNumber: attemptNumber,
                patternKey: template.Key,
                cefrLevel: context.CefrLevelOverride ?? template.CefrLevel,
                objectiveKey: template.CurriculumObjectiveKey,
                providerName: providerName,
                modelName: modelName,
                correlationId: correlationId,
                studentProfileId: context.StudentProfileId);

            _db.GenerationValidationFailures.Add(failure);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist generation validation failure record for template {TemplateKey} (non-blocking).", template.Key);
        }
    }
}
