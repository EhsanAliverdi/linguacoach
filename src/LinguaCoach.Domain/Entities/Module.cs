using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H5 — a reusable, reviewable learning unit combining one or more <see cref="Lesson"/>s
/// and <see cref="Exercise"/>s plus a module-level feedback plan: the top of the
/// content-studio hierarchy (`Resource Bank Item → Lesson/Exercise → Module
/// Definition`, see docs/architecture/product-model-realignment-h0.md). Always starts
/// <see cref="AdminReviewStatus.PendingReview"/> — nothing here is ever auto-published or
/// delivered to a student; H6 (Daily Lesson) and H7 (Practice Gym) are the eventual runtime
/// consumers, not built yet.
///
/// <b>Not the same as <see cref="LearningModule"/></b>, an existing per-student runtime entity (a
/// thematic group of <see cref="LearningActivity"/> rows within a <see cref="LearningPath"/>,
/// tracks its own completion). <see cref="Module"/> is a reusable design with no
/// per-student state and is <b>not wired into any runtime selection/delivery path</b> in this
/// phase — mirrors the same naming-collision-avoidance decision H4 made for
/// <see cref="Exercise"/> vs <see cref="LearningActivity"/>/<see cref="ActivityTemplate"/>.
/// </summary>
public sealed class Module : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    // ObjectiveKey (free-text alignment to the retired CurriculumObjective taxonomy) was removed
    // in Adaptive Curriculum Sprint 7 — real Module-to-competency alignment is now
    // ModuleSkillGraphNodeLink, a validated many-to-many relationship, not a free-text field.

    public string? CefrLevel { get; private set; }
    public string? Skill { get; private set; }
    public string? Subskill { get; private set; }
    public string ContextTagsJson { get; private set; } = "[]";
    public string FocusTagsJson { get; private set; } = "[]";
    public int? DifficultyBand { get; private set; }
    public int? EstimatedMinutes { get; private set; }

    /// <summary>Backend-only, module-level feedback plan (e.g. completion message), distinct from
    /// each linked Activity's own <see cref="Exercise.FeedbackPlanJson"/>. Never sent to
    /// students by H5 itself — no runtime wiring exists yet.</summary>
    public string? FeedbackPlanJson { get; private set; }

    public ModuleSourceMode SourceMode { get; private set; }

    /// <summary>"Deterministic" for the H5 draft composer, or an AI provider name once real AI
    /// generation is wired in (not implemented this phase).</summary>
    public string? GenerationProvider { get; private set; }
    public string? GenerationModel { get; private set; }

    public AdminReviewStatus ReviewStatus { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }
    public DateTimeOffset? RejectedAtUtc { get; private set; }
    public string? RejectionReason { get; private set; }
    public string? ReviewNotes { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Phase K6 — admin-facing soft-delete, mirroring <see cref="ResourceBankItem.IsArchived"/>.
    /// Archiving a Module never cascades to its linked Lessons/Exercises — it only hides this
    /// Module row from the default admin list. Safe even once real students have been assigned
    /// (StudentTodayPlanModuleAssignment/StudentPracticeGymModuleAssignment/StudentExerciseLaunch
    /// keep resolving; archiving never deletes the row).</summary>
    public bool IsArchived { get; private set; }

    private Module() { }

    public Module(
        string title,
        ModuleSourceMode sourceMode,
        string? description = null,
        string? cefrLevel = null,
        string? skill = null,
        string? subskill = null,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        int? difficultyBand = null,
        int? estimatedMinutes = null,
        string? feedbackPlanJson = null,
        string? generationProvider = null,
        string? generationModel = null,
        Guid? createdByUserId = null)
    {
        ValidateAuthorableFields(title, cefrLevel, difficultyBand, estimatedMinutes);

        Title = title.Trim();
        SourceMode = sourceMode;
        Description = description?.Trim();
        CefrLevel = cefrLevel?.Trim().ToUpperInvariant();
        Skill = skill?.Trim();
        Subskill = subskill?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        DifficultyBand = difficultyBand;
        EstimatedMinutes = estimatedMinutes;
        FeedbackPlanJson = feedbackPlanJson;
        GenerationProvider = generationProvider?.Trim();
        GenerationModel = generationModel?.Trim();
        CreatedByUserId = createdByUserId;
        ReviewStatus = AdminReviewStatus.PendingReview;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Edits draft content/metadata. Blocked once approved — same policy as
    /// <see cref="Lesson.UpdateDraft"/>/<see cref="Exercise.UpdateDraft"/>: reject
    /// first to reopen editing. Editing a rejected draft resubmits it.</summary>
    public void UpdateDraft(
        string title,
        string? description,
        string? cefrLevel,
        string? skill,
        string? subskill,
        string contextTagsJson,
        string focusTagsJson,
        int? difficultyBand,
        int? estimatedMinutes,
        string? feedbackPlanJson)
    {
        if (ReviewStatus == AdminReviewStatus.Approved)
            throw new InvalidOperationException(
                $"Cannot edit Module '{Title}': it is already approved. Reject it first to reopen editing.");

        ValidateAuthorableFields(title, cefrLevel, difficultyBand, estimatedMinutes);

        Title = title.Trim();
        Description = description?.Trim();
        CefrLevel = cefrLevel?.Trim().ToUpperInvariant();
        Skill = skill?.Trim();
        Subskill = subskill?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        DifficultyBand = difficultyBand;
        EstimatedMinutes = estimatedMinutes;
        FeedbackPlanJson = feedbackPlanJson;

        if (ReviewStatus == AdminReviewStatus.Rejected)
        {
            ReviewStatus = AdminReviewStatus.PendingReview;
            RejectionReason = null;
        }

        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Approve(Guid? reviewedByUserId, string? notes = null)
    {
        ReviewStatus = AdminReviewStatus.Approved;
        ReviewedByUserId = reviewedByUserId;
        ApprovedAtUtc = DateTimeOffset.UtcNow;
        RejectedAtUtc = null;
        RejectionReason = null;
        if (notes is not null)
            ReviewNotes = notes.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Reject(string reason, Guid? reviewedByUserId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to reject a Module.", nameof(reason));

        ReviewStatus = AdminReviewStatus.Rejected;
        ReviewedByUserId = reviewedByUserId;
        RejectedAtUtc = DateTimeOffset.UtcNow;
        RejectionReason = reason.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsArchived = true;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Unarchive()
    {
        IsArchived = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static void ValidateAuthorableFields(
        string title, string? cefrLevel, int? difficultyBand, int? estimatedMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (cefrLevel is not null && !CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.", nameof(cefrLevel));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (estimatedMinutes is < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedMinutes), "EstimatedMinutes must be >= 0.");
    }
}
