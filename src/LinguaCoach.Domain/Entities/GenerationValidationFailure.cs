using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Persisted record of a content validation failure during AI activity generation.
/// Written when ModuleStageContentValidator rejects AI output (first attempt or retry).
/// Append-only — never mutated after creation.
/// </summary>
public sealed class GenerationValidationFailure : BaseEntity
{
    /// <summary>Exercise pattern key e.g. "email_reply". Null for legacy unkeyed generation.</summary>
    public string? PatternKey { get; private set; }

    /// <summary>ActivityType enum name at generation time.</summary>
    public string ActivityTypeName { get; private set; }

    /// <summary>CEFR level at generation time e.g. "B1".</summary>
    public string? CefrLevel { get; private set; }

    /// <summary>Objective key if routing was objective-driven.</summary>
    public string? ObjectiveKey { get; private set; }

    /// <summary>Provider name if known at failure time.</summary>
    public string? ProviderName { get; private set; }

    /// <summary>Model name if known at failure time.</summary>
    public string? ModelName { get; private set; }

    /// <summary>Semicolon-delimited validation error messages (safe — never contains raw AI output).</summary>
    public string ValidationErrors { get; private set; }

    /// <summary>1 = first attempt failed; 2 = retry also failed (generation abandoned).</summary>
    public int AttemptNumber { get; private set; }

    /// <summary>Request correlation ID from AiExecutionService if available.</summary>
    public string? CorrelationId { get; private set; }

    /// <summary>Student profile that triggered generation. Null for batch/pool generation without a specific student.</summary>
    public Guid? StudentProfileId { get; private set; }

    private GenerationValidationFailure()
    {
        ActivityTypeName = string.Empty;
        ValidationErrors = string.Empty;
    }

    public GenerationValidationFailure(
        string activityTypeName,
        string validationErrors,
        int attemptNumber,
        string? patternKey = null,
        string? cefrLevel = null,
        string? objectiveKey = null,
        string? providerName = null,
        string? modelName = null,
        string? correlationId = null,
        Guid? studentProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(activityTypeName))
            throw new ArgumentException("ActivityTypeName is required.", nameof(activityTypeName));
        if (string.IsNullOrWhiteSpace(validationErrors))
            throw new ArgumentException("ValidationErrors is required.", nameof(validationErrors));
        if (attemptNumber < 1 || attemptNumber > 2)
            throw new ArgumentOutOfRangeException(nameof(attemptNumber), "AttemptNumber must be 1 or 2.");

        ActivityTypeName = activityTypeName.Trim();
        ValidationErrors = validationErrors.Trim();
        AttemptNumber = attemptNumber;
        PatternKey = string.IsNullOrWhiteSpace(patternKey) ? null : patternKey.Trim();
        CefrLevel = string.IsNullOrWhiteSpace(cefrLevel) ? null : cefrLevel.Trim();
        ObjectiveKey = string.IsNullOrWhiteSpace(objectiveKey) ? null : objectiveKey.Trim();
        ProviderName = string.IsNullOrWhiteSpace(providerName) ? null : providerName.Trim();
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim();
        CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? null : correlationId.Trim();
        StudentProfileId = studentProfileId;
    }
}
