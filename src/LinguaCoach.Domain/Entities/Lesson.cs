using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H3 — a reviewable teaching/explanation block: the "Learn" half of a future Module
/// (<c>Resource Bank Item → Lesson/Activity → Module</c>, see
/// docs/architecture/product-model-realignment-h0.md). Generated from one or more selected
/// published Resource Bank rows (see <see cref="LessonResourceLink"/> for traceability) or
/// authored manually. Always starts <see cref="AdminReviewStatus.PendingReview"/> — nothing here
/// is ever auto-published; a future Module (Phase H5) is the eventual consumer, not built yet.
/// Reuses <see cref="AdminReviewStatus"/> rather than a new status enum, mirroring
/// <see cref="ActivityTemplate"/>/<see cref="ResourceCandidate"/>'s existing review-status
/// convention.
/// </summary>
public sealed class Lesson : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>JSON array of example strings.</summary>
    public string ExamplesJson { get; private set; } = "[]";

    /// <summary>JSON array of common-mistake strings.</summary>
    public string CommonMistakesJson { get; private set; } = "[]";
    public string? UsageNotes { get; private set; }

    public string? CefrLevel { get; private set; }
    public string? Skill { get; private set; }
    public string? Subskill { get; private set; }
    public string ContextTagsJson { get; private set; } = "[]";
    public string FocusTagsJson { get; private set; } = "[]";
    public int? DifficultyBand { get; private set; }
    public int? EstimatedMinutes { get; private set; }

    public LessonSourceMode SourceMode { get; private set; }

    /// <summary>"Deterministic" for the H3 draft composer, or an AI provider name once real AI
    /// generation is wired in (see docs — not implemented this phase).</summary>
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
    /// Archived Lessons are excluded from the default admin list but the row and every link into it
    /// (LessonResourceLink, ModuleLessonLink, Exercise.LessonId) stay intact — archiving never
    /// breaks an Exercise/Module that already references this Lesson, it only hides the row.</summary>
    public bool IsArchived { get; private set; }

    private Lesson() { }

    public Lesson(
        string title,
        string body,
        LessonSourceMode sourceMode,
        string? cefrLevel = null,
        string? skill = null,
        string? subskill = null,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        string examplesJson = "[]",
        string commonMistakesJson = "[]",
        string? usageNotes = null,
        int? difficultyBand = null,
        int? estimatedMinutes = null,
        string? generationProvider = null,
        string? generationModel = null,
        Guid? createdByUserId = null)
    {
        ValidateAuthorableFields(title, body, cefrLevel, difficultyBand, estimatedMinutes);

        Title = title.Trim();
        Body = body.Trim();
        SourceMode = sourceMode;
        CefrLevel = cefrLevel?.Trim().ToUpperInvariant();
        Skill = skill?.Trim();
        Subskill = subskill?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        ExamplesJson = string.IsNullOrWhiteSpace(examplesJson) ? "[]" : examplesJson;
        CommonMistakesJson = string.IsNullOrWhiteSpace(commonMistakesJson) ? "[]" : commonMistakesJson;
        UsageNotes = usageNotes?.Trim();
        DifficultyBand = difficultyBand;
        EstimatedMinutes = estimatedMinutes;
        GenerationProvider = generationProvider?.Trim();
        GenerationModel = generationModel?.Trim();
        CreatedByUserId = createdByUserId;
        ReviewStatus = AdminReviewStatus.PendingReview;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Edits draft content/metadata. Blocked once approved — an approved Lesson is a
    /// reviewed, stable artifact; reject it first (which reopens editing) rather than silently
    /// mutating already-approved content. Editing a rejected draft resubmits it (moves
    /// <see cref="ReviewStatus"/> back to <see cref="AdminReviewStatus.PendingReview"/>).</summary>
    public void UpdateDraft(
        string title,
        string body,
        string examplesJson,
        string commonMistakesJson,
        string? usageNotes,
        string? cefrLevel,
        string? skill,
        string? subskill,
        string contextTagsJson,
        string focusTagsJson,
        int? difficultyBand,
        int? estimatedMinutes)
    {
        if (ReviewStatus == AdminReviewStatus.Approved)
            throw new InvalidOperationException(
                $"Cannot edit Lesson '{Title}': it is already approved. Reject it first to reopen editing.");

        ValidateAuthorableFields(title, body, cefrLevel, difficultyBand, estimatedMinutes);

        Title = title.Trim();
        Body = body.Trim();
        ExamplesJson = string.IsNullOrWhiteSpace(examplesJson) ? "[]" : examplesJson;
        CommonMistakesJson = string.IsNullOrWhiteSpace(commonMistakesJson) ? "[]" : commonMistakesJson;
        UsageNotes = usageNotes?.Trim();
        CefrLevel = cefrLevel?.Trim().ToUpperInvariant();
        Skill = skill?.Trim();
        Subskill = subskill?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        DifficultyBand = difficultyBand;
        EstimatedMinutes = estimatedMinutes;

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
            throw new ArgumentException("Reason is required to reject a Lesson.", nameof(reason));

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
        string title, string body, string? cefrLevel, int? difficultyBand, int? estimatedMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));
        if (cefrLevel is not null && !CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.", nameof(cefrLevel));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (estimatedMinutes is < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedMinutes), "EstimatedMinutes must be >= 0.");
    }
}
