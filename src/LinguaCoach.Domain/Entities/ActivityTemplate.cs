using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A reusable, admin-authored learning activity template — the bank-first counterpart to
/// today's per-student, throwaway <see cref="LearningActivity"/> AI generation. AI personalizes
/// instances from a template (Phase 5+); the template itself defines the approved pattern,
/// CEFR/skill/subskill metadata, backend-only scoring model, and validation rules.
///
/// Phase 4 (docs/reviews/2026-07-07-ai-bank-assessment-architecture-plan.md) is hand-authored
/// only — no AI generation wires into this entity yet. <see cref="FormIoBaseSchemaJson"/> is
/// student-safe; <see cref="ScoringModelJson"/> is backend-only and must never appear in any
/// student-facing DTO, mirroring the PlacementItemDefinition split.
/// </summary>
public sealed class ActivityTemplate : BaseEntity
{
    /// <summary>Stable identifier shared across versions of the same template.</summary>
    public string Key { get; private set; } = string.Empty;

    public int VersionNumber { get; private set; } = 1;

    /// <summary>Id of the ActivityTemplate row this version was derived from, if any.</summary>
    public Guid? PreviousVersionId { get; private set; }

    public string Skill { get; private set; } = string.Empty;
    public string? Subskill { get; private set; }
    public string CefrLevel { get; private set; } = string.Empty;

    /// <summary>JSON array of CurriculumContextTagConstants values.</summary>
    public string ContextTagsJson { get; private set; } = "[]";

    /// <summary>JSON array of free-text focus area tags.</summary>
    public string FocusTagsJson { get; private set; } = "[]";

    public string? CurriculumObjectiveKey { get; private set; }

    public string ActivityType { get; private set; } = string.Empty;
    public string? PatternKey { get; private set; }

    /// <summary>Student-safe Form.io base schema. Never contains a correct answer or scoring data.</summary>
    public string? FormIoBaseSchemaJson { get; private set; }

    /// <summary>Prompt fragment/constraints an AI generation pipeline personalizes from (Phase 5+). Not shown to students.</summary>
    public string? GenerationInstructions { get; private set; }

    /// <summary>Backend-only: scoring model for this template, keyed by Form.io component key. Never returned to students.</summary>
    public string? ScoringModelJson { get; private set; }

    /// <summary>Backend-only: additional validation constraints an AI-personalized instance must satisfy (Phase 5+).</summary>
    public string? ValidationRulesJson { get; private set; }

    public AdminReviewStatus ReviewStatus { get; private set; } = AdminReviewStatus.NotRequired;

    /// <summary>True once an admin has published this template version for use. A rejected
    /// template cannot be published.</summary>
    public bool IsPublished { get; private set; }

    public int? EstimatedDurationSeconds { get; private set; }

    /// <summary>JSON array of asset requirement tags, e.g. ["tts_audio"], ["image_prompt"].</summary>
    public string? AssetRequirementsJson { get; private set; }

    private ActivityTemplate() { }

    public ActivityTemplate(
        string key,
        string skill,
        string cefrLevel,
        string activityType,
        string? subskill = null,
        string? patternKey = null,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        string? curriculumObjectiveKey = null,
        string? formIoBaseSchemaJson = null,
        string? generationInstructions = null,
        string? scoringModelJson = null,
        string? validationRulesJson = null,
        int? estimatedDurationSeconds = null,
        string? assetRequirementsJson = null,
        int versionNumber = 1,
        Guid? previousVersionId = null)
    {
        ValidateAuthorableFields(key, skill, cefrLevel, activityType, subskill, estimatedDurationSeconds);

        Key = key.Trim();
        Skill = skill.ToLowerInvariant().Trim();
        Subskill = subskill?.Trim().ToLowerInvariant();
        CefrLevel = cefrLevel.ToUpperInvariant().Trim();
        ActivityType = activityType.Trim();
        PatternKey = patternKey?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        CurriculumObjectiveKey = curriculumObjectiveKey?.Trim();
        FormIoBaseSchemaJson = formIoBaseSchemaJson;
        GenerationInstructions = generationInstructions?.Trim();
        ScoringModelJson = scoringModelJson;
        ValidationRulesJson = validationRulesJson;
        EstimatedDurationSeconds = estimatedDurationSeconds;
        AssetRequirementsJson = assetRequirementsJson;
        VersionNumber = versionNumber;
        PreviousVersionId = previousVersionId;
    }

    /// <summary>Updates authorable metadata fields only. Does not touch schema/scoring
    /// (use <see cref="SetSchema"/>), review status, or publish state.</summary>
    public void Update(
        string skill,
        string cefrLevel,
        string activityType,
        string? subskill,
        string? patternKey,
        string contextTagsJson,
        string focusTagsJson,
        string? curriculumObjectiveKey,
        int? estimatedDurationSeconds,
        string? assetRequirementsJson)
    {
        ValidateAuthorableFields(Key, skill, cefrLevel, activityType, subskill, estimatedDurationSeconds);

        Skill = skill.ToLowerInvariant().Trim();
        CefrLevel = cefrLevel.ToUpperInvariant().Trim();
        ActivityType = activityType.Trim();
        Subskill = subskill?.Trim().ToLowerInvariant();
        PatternKey = patternKey?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        CurriculumObjectiveKey = curriculumObjectiveKey?.Trim();
        EstimatedDurationSeconds = estimatedDurationSeconds;
        AssetRequirementsJson = assetRequirementsJson;
    }

    /// <summary>Sets the Form.io base schema and backend-only scoring/validation/generation
    /// fields independently of <see cref="Update"/>.</summary>
    public void SetSchema(
        string? formIoBaseSchemaJson,
        string? scoringModelJson,
        string? validationRulesJson,
        string? generationInstructions)
    {
        FormIoBaseSchemaJson = formIoBaseSchemaJson;
        ScoringModelJson = scoringModelJson;
        ValidationRulesJson = validationRulesJson;
        GenerationInstructions = generationInstructions?.Trim();
    }

    public void Approve(string? notes = null) => ReviewStatus = AdminReviewStatus.Approved;

    public void Reject(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to reject a template.", nameof(reason));

        ReviewStatus = AdminReviewStatus.Rejected;
        IsPublished = false;
    }

    public void ResetToPendingReview() => ReviewStatus = AdminReviewStatus.PendingReview;

    public void Publish()
    {
        if (ReviewStatus == AdminReviewStatus.Rejected)
            throw new InvalidOperationException($"Cannot publish template '{Key}': it is rejected.");

        IsPublished = true;
    }

    public void Unpublish() => IsPublished = false;

    private static void ValidateAuthorableFields(
        string key, string skill, string cefrLevel, string activityType,
        string? subskill, int? estimatedDurationSeconds)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (!CurriculumSkillConstants.IsValid(skill))
            throw new ArgumentException($"Invalid skill '{skill}'. Must be one of: {string.Join(", ", CurriculumSkillConstants.All)}.", nameof(skill));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(activityType))
            throw new ArgumentException("ActivityType is required.", nameof(activityType));
        if (!CurriculumSubskillConstants.IsValidForSkill(skill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{skill}'.", nameof(subskill));
        if (estimatedDurationSeconds is < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedDurationSeconds), "EstimatedDurationSeconds must be >= 0.");
    }
}
