using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;

namespace LinguaCoach.Domain.Entities;

/*
 * Curriculum objective lifecycle:
 *
 *   Active  ──Deactivate()──► Inactive
 *   Inactive ──Activate()───► Active
 *
 * IsReviewable = true means this objective can appear in review sessions
 * for students who have already passed it.
 *
 * PrerequisiteKeysJson = JSON array of other CurriculumObjective.Key values.
 * Self-reference is rejected at construction time.
 * Cross-reference integrity is validated by CurriculumObjectiveSeeder after all
 * objectives are seeded.
 */

/// <summary>
/// Defines a single curriculum learning objective.
/// Scoped by CEFR level, skill, learner context tags, and focus area tags.
/// Used by ICurriculumSyllabusQuery to return candidate objectives for a student context.
/// This entity does NOT select activities or exercise formats — that belongs to 10L routing.
/// </summary>
public sealed class CurriculumObjective : BaseEntity
{
    /// <summary>Stable unique identifier. Used as FK in prerequisites and as a query handle.</summary>
    public string Key { get; private set; }

    /// <summary>Human-readable title shown in admin and diagnostics.</summary>
    public string Title { get; private set; }

    /// <summary>Short description of what this objective teaches.</summary>
    public string Description { get; private set; }

    /// <summary>CEFR level this objective targets. Must be a value from CefrLevelConstants.</summary>
    public string CefrLevel { get; private set; }

    /// <summary>Primary skill. Must be a value from CurriculumSkillConstants.</summary>
    public string PrimarySkill { get; private set; }

    /// <summary>JSON array of secondary skill strings from CurriculumSkillConstants.</summary>
    public string SecondarySkillsJson { get; private set; }

    /// <summary>Optional finer-grained classification beneath PrimarySkill. Null means unclassified.
    /// Must be a value from CurriculumSubskillConstants belonging to PrimarySkill when set.</summary>
    public string? Subskill { get; private set; }

    /// <summary>JSON array of CurriculumContextTagConstants values this objective suits.</summary>
    public string ContextTagsJson { get; private set; }

    /// <summary>JSON array of free-text focus area tags (e.g. "email_writing", "job_search").</summary>
    public string FocusTagsJson { get; private set; }

    /// <summary>JSON array of prerequisite CurriculumObjective.Key values.</summary>
    public string PrerequisiteKeysJson { get; private set; }

    /// <summary>Lower value = introduced earlier. Used for ordering candidate lists.</summary>
    public int RecommendedOrder { get; private set; }

    /// <summary>Difficulty band 1 (easiest) to 5 (hardest) within the CEFR level.</summary>
    public int DifficultyBand { get; private set; }

    /// <summary>When false, excluded from active queries. Allows soft-disabling without deletion.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When true, this objective may be surfaced in review sessions.</summary>
    public bool IsReviewable { get; private set; }

    /// <summary>When true, this objective uses an exam-style question format.</summary>
    public bool IsExamInspired { get; private set; }

    /// <summary>Optional teaching notes for AI prompt context. Not shown to students.</summary>
    public string? TeachingNotes { get; private set; }

    /// <summary>Optional example prompts for AI generation context. Not shown to students.</summary>
    public string? ExamplePrompts { get; private set; }

    /// <summary>UTC timestamp when this objective was last modified by an admin (not the seeder).</summary>
    public DateTimeOffset? AdminUpdatedAt { get; private set; }

    private CurriculumObjective()
    {
        Key = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
        CefrLevel = string.Empty;
        PrimarySkill = string.Empty;
        SecondarySkillsJson = "[]";
        ContextTagsJson = "[]";
        FocusTagsJson = "[]";
        PrerequisiteKeysJson = "[]";
        ExamplePrompts = null;
        AdminUpdatedAt = null;
    }

    public CurriculumObjective(
        string key,
        string title,
        string description,
        string cefrLevel,
        string primarySkill,
        string secondarySkillsJson = "[]",
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        string prerequisiteKeysJson = "[]",
        int recommendedOrder = 0,
        int difficultyBand = 1,
        bool isActive = true,
        bool isReviewable = false,
        bool isExamInspired = false,
        string? teachingNotes = null,
        string? examplePrompts = null,
        string? subskill = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.", nameof(cefrLevel));
        if (!CurriculumSkillConstants.IsValid(primarySkill))
            throw new ArgumentException($"Invalid primary skill '{primarySkill}'. Must be one of: {string.Join(", ", CurriculumSkillConstants.All)}.", nameof(primarySkill));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (recommendedOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(recommendedOrder), "RecommendedOrder must be >= 0.");
        if (!CurriculumSubskillConstants.IsValidForSkill(primarySkill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{primarySkill}'.", nameof(subskill));

        // Self-prerequisite guard (key-level; cross-ref integrity done by seeder)
        ValidateNoSelfPrerequisite(key, prerequisiteKeysJson);

        Key = key.Trim();
        Title = title.Trim();
        Description = description.Trim();
        CefrLevel = cefrLevel.ToUpperInvariant();
        PrimarySkill = primarySkill.ToLowerInvariant();
        SecondarySkillsJson = secondarySkillsJson;
        Subskill = subskill?.Trim().ToLowerInvariant();
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
        PrerequisiteKeysJson = prerequisiteKeysJson;
        RecommendedOrder = recommendedOrder;
        DifficultyBand = difficultyBand;
        IsActive = isActive;
        IsReviewable = isReviewable;
        IsExamInspired = isExamInspired;
        TeachingNotes = teachingNotes?.Trim();
        ExamplePrompts = examplePrompts?.Trim();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;

    /// <summary>Seeder-safe update: only title/description/order/difficulty/notes. Does not touch admin fields.</summary>
    public void UpdateDetails(
        string title,
        string description,
        int recommendedOrder,
        int difficultyBand,
        string? teachingNotes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (recommendedOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(recommendedOrder), "RecommendedOrder must be >= 0.");

        Title = title.Trim();
        Description = description.Trim();
        RecommendedOrder = recommendedOrder;
        DifficultyBand = difficultyBand;
        TeachingNotes = teachingNotes?.Trim();
    }

    /// <summary>Admin full update: updates all mutable fields including skills, tags, prerequisites.</summary>
    public void AdminUpdate(
        string title,
        string description,
        string cefrLevel,
        string primarySkill,
        string secondarySkillsJson,
        string contextTagsJson,
        string focusTagsJson,
        string prerequisiteKeysJson,
        int recommendedOrder,
        int difficultyBand,
        bool isReviewable,
        bool isExamInspired,
        string? teachingNotes,
        string? examplePrompts)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'.", nameof(cefrLevel));
        if (!CurriculumSkillConstants.IsValid(primarySkill))
            throw new ArgumentException($"Invalid primary skill '{primarySkill}'.", nameof(primarySkill));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (recommendedOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(recommendedOrder), "RecommendedOrder must be >= 0.");

        ValidateNoSelfPrerequisite(Key, prerequisiteKeysJson);

        Title = title.Trim();
        Description = description.Trim();
        CefrLevel = cefrLevel.ToUpperInvariant();
        PrimarySkill = primarySkill.ToLowerInvariant();
        SecondarySkillsJson = secondarySkillsJson;
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
        PrerequisiteKeysJson = prerequisiteKeysJson;
        RecommendedOrder = recommendedOrder;
        DifficultyBand = difficultyBand;
        IsReviewable = isReviewable;
        IsExamInspired = isExamInspired;
        TeachingNotes = teachingNotes?.Trim();
        ExamplePrompts = examplePrompts?.Trim();
        AdminUpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Sets or clears the subskill independently of AdminUpdate, so an admin edit that
    /// doesn't touch subskill never silently resets it.</summary>
    public void SetSubskill(string? subskill)
    {
        if (!CurriculumSubskillConstants.IsValidForSkill(PrimarySkill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{PrimarySkill}'.", nameof(subskill));

        Subskill = subskill?.Trim().ToLowerInvariant();
    }

    private static void ValidateNoSelfPrerequisite(string key, string prerequisiteKeysJson)
    {
        if (string.IsNullOrWhiteSpace(prerequisiteKeysJson) || prerequisiteKeysJson == "[]")
            return;

        // Simple string check — avoids a JSON parse dependency in the domain layer.
        // The key is surrounded by quotes in the JSON array.
        if (prerequisiteKeysJson.Contains($"\"{key}\"", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Objective '{key}' cannot list itself as a prerequisite.", nameof(prerequisiteKeysJson));
    }
}
