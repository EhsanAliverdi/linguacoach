using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Sprint 1 of the Adaptive Curriculum initiative — a discrete teachable competency (a grammar
/// structure, a functional-language chunk, a vocabulary set, a subskill) in the skill/prerequisite
/// graph that replaces the flat <see cref="CurriculumObjective"/> taxonomy. Nodes are AI-drafted
/// (see docs/architecture/adaptive-curriculum-skill-graph.md) and admin-approved in a batch, never
/// per-node — reuses <see cref="Enums.AdminReviewStatus"/>, the same PendingReview→Approved/Rejected
/// convention <see cref="Module"/>/<see cref="Lesson"/>/<see cref="Exercise"/> already use, rather
/// than inventing a new status enum.
/// </summary>
public sealed class SkillGraphNode : BaseEntity
{
    /// <summary>Stable unique identifier, e.g. "grammar.present_simple.a1". Used as a human-legible
    /// handle in AI prompts/logs; prerequisite edges reference nodes by Id (Guid), not by key.</summary>
    public string Key { get; private set; }

    public string Title { get; private set; }

    /// <summary>Short description of what this node teaches — shown to admins and given to the AI
    /// composer as reasoning context. Not shown to students.</summary>
    public string Description { get; private set; }

    /// <summary>CEFR level this node targets. Must be a value from CefrLevelConstants.</summary>
    public string CefrLevel { get; private set; }

    /// <summary>Must be a value from CurriculumSkillConstants.</summary>
    public string Skill { get; private set; }

    /// <summary>Optional finer-grained classification beneath Skill. Must be a value from
    /// CurriculumSubskillConstants belonging to Skill when set.</summary>
    public string? Subskill { get; private set; }

    /// <summary>Difficulty band 1 (easiest) to 5 (hardest) within the CEFR level.</summary>
    public int DifficultyBand { get; private set; }

    /// <summary>AI-drafted rationale for the node/its prerequisites — teaching context for the AI
    /// composer, not shown to students. Distinct from Description (student-legible-if-ever-shown
    /// summary) vs. this (internal reasoning aid).</summary>
    public string? DescriptionForAi { get; private set; }

    public AdminReviewStatus ReviewStatus { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>When false, excluded from active graph queries (mastery/composer). Allows
    /// soft-disabling without deletion — same convention as CurriculumObjective.IsActive.</summary>
    public bool IsActive { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    private SkillGraphNode() { }

    public SkillGraphNode(
        string key,
        string title,
        string description,
        string cefrLevel,
        string skill,
        string? subskill = null,
        int difficultyBand = 1,
        string? descriptionForAi = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (!CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.", nameof(cefrLevel));
        if (!CurriculumSkillConstants.IsValid(skill))
            throw new ArgumentException($"Invalid skill '{skill}'. Must be one of: {string.Join(", ", CurriculumSkillConstants.All)}.", nameof(skill));
        if (!CurriculumSubskillConstants.IsValidForSkill(skill, subskill))
            throw new ArgumentException($"Subskill '{subskill}' does not belong to skill '{skill}'.", nameof(subskill));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");

        Key = key.Trim();
        Title = title.Trim();
        Description = description.Trim();
        CefrLevel = cefrLevel.ToUpperInvariant();
        Skill = skill.ToLowerInvariant();
        Subskill = subskill?.Trim().ToLowerInvariant();
        DifficultyBand = difficultyBand;
        DescriptionForAi = descriptionForAi?.Trim();
        ReviewStatus = AdminReviewStatus.PendingReview;
        IsActive = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(Guid? reviewedByUserId)
    {
        ReviewStatus = AdminReviewStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        RejectedAtUtc = null;
        RejectionReason = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string reason, Guid? reviewedByUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to reject a skill-graph node.", nameof(reason));

        ReviewStatus = AdminReviewStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        RejectedAtUtc = DateTimeOffset.UtcNow;
        RejectionReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
