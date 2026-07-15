using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Constants;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Phase H4 — a reviewable, editable practice task design: the "Practice" half of a future Module
/// (`Resource Bank Item → Lesson/Activity → Module`, see
/// docs/architecture/product-model-realignment-h0.md). Generated from one or more selected
/// published Resource Bank rows (see <see cref="ExerciseResourceLink"/>) or from an existing
/// <see cref="Lesson"/>, or authored manually. Always starts
/// <see cref="AdminReviewStatus.PendingReview"/> — nothing here is ever auto-published; a future
/// Module (Phase H5) is the eventual consumer, not built yet.
///
/// <b>Not the same as two existing entities with similar names:</b>
/// <list type="bullet">
/// <item><description><see cref="LearningActivity"/> is a per-student runtime/delivery record
/// (Today materialization, Practice Gym) — <see cref="Exercise"/> is a reusable design,
/// not tied to any student.</description></item>
/// <item><description><see cref="ActivityTemplate"/> is an existing admin-authored, AI-personalized
/// template already wired into the live Practice Gym Form.io pilot runtime
/// (<c>PracticeGymGenerationJob.TemplateMigratedPatternKeys</c>) — <see cref="Exercise"/>
/// is a new, separate H4 foundation-phase entity with Resource Bank/Lesson traceability that
/// <see cref="ActivityTemplate"/> does not have, and is deliberately NOT wired into any runtime
/// selection/delivery path in this phase.</description></item>
/// </list>
/// </summary>
public sealed class Exercise : BaseEntity
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>Student-facing prompt/instructions shown with the exercise.</summary>
    public string Instructions { get; private set; } = string.Empty;

    /// <summary>e.g. "gap_fill", "multiple_choice_single", "short_answer" — see
    /// <see cref="Infrastructure.Lessons"/> sibling namespace's Activity generation service for
    /// the initial supported set. Free-text, not enum-constrained (mirrors
    /// <see cref="ActivityTemplate.ActivityType"/>'s convention) so future types don't need a
    /// migration.</summary>
    public string ActivityType { get; private set; } = string.Empty;

    /// <summary>Optional alignment to an existing <see cref="ExercisePatternDefinition"/> pattern
    /// key, for future runtime integration. Null for H4-generated drafts.</summary>
    public string? PatternKey { get; private set; }

    public ExerciseRendererType RendererType { get; private set; }

    /// <summary>Student-safe Form.io schema JSON — never contains a correct answer or scoring
    /// data (validated by <c>IFormIoSchemaValidationService</c> at generation/save time). Null for
    /// non-Formio renderer types.</summary>
    public string? FormSchemaJson { get; private set; }

    /// <summary>Backend-only, admin-readable summary of the correct answer(s) — denormalized from
    /// <see cref="ScoringRulesJson"/> for quick display, never sent to students. E.g.
    /// <c>{"word_answer":"resilient"}</c>.</summary>
    public string? AnswerKeyJson { get; private set; }

    /// <summary>Backend-only. Shape matches the existing shared
    /// <c>LinguaCoach.Application.Placement.ScoringRulesDocument</c> (component key →
    /// <c>ComponentScoringRule</c>) already used by placement/onboarding/reorder_paragraphs
    /// scoring, so a future runtime integration can reuse
    /// <c>LinguaCoach.Application.FormIo.ComponentAnswerScorer</c> as-is rather than inventing a
    /// second scoring format.</summary>
    public string? ScoringRulesJson { get; private set; }

    /// <summary>Backend-only static feedback plan, e.g.
    /// <c>{"correctFeedback":"...","incorrectFeedback":"..."}</c>. Never sent to students by H4
    /// itself — no runtime wiring exists yet.</summary>
    public string? FeedbackPlanJson { get; private set; }

    public string? CefrLevel { get; private set; }
    public string? Skill { get; private set; }
    public string? Subskill { get; private set; }
    public string ContextTagsJson { get; private set; } = "[]";
    public string FocusTagsJson { get; private set; } = "[]";
    public int? DifficultyBand { get; private set; }
    public int? EstimatedMinutes { get; private set; }

    /// <summary>The <see cref="Lesson"/> this Activity practices or assesses — mandatory since
    /// Phase 2 (2026-07-15 exercise pipeline boundary consolidation). Every Exercise, generated or
    /// manually authored, must have exactly one instructional parent Lesson; there is no supported
    /// path from Resource Bank items directly to an Exercise. <see cref="ExerciseResourceLink"/> is
    /// separate source provenance and does not replace this.</summary>
    public Guid LessonId { get; private set; }

    public ExerciseSourceMode SourceMode { get; private set; }

    /// <summary>"Deterministic" for the H4 draft composer, or an AI provider name once real AI
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
    /// Archived Exercises are excluded from the default admin list but the row and every link into
    /// it (ExerciseResourceLink, ModuleExerciseLink) stay intact — archiving never breaks a Module
    /// that already references this Exercise, it only hides the row.</summary>
    public bool IsArchived { get; private set; }

    private Exercise() { }

    public Exercise(
        string title,
        string instructions,
        string activityType,
        ExerciseRendererType rendererType,
        ExerciseSourceMode sourceMode,
        string? description = null,
        string? patternKey = null,
        string? formSchemaJson = null,
        string? answerKeyJson = null,
        string? scoringRulesJson = null,
        string? feedbackPlanJson = null,
        string? cefrLevel = null,
        string? skill = null,
        string? subskill = null,
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        int? difficultyBand = null,
        int? estimatedMinutes = null,
        Guid lessonId = default,
        string? generationProvider = null,
        string? generationModel = null,
        Guid? createdByUserId = null)
    {
        ValidateAuthorableFields(title, instructions, activityType, cefrLevel, difficultyBand, estimatedMinutes);
        if (lessonId == Guid.Empty)
            throw new ArgumentException("LessonId is required — every Exercise must have a Lesson.", nameof(lessonId));

        Title = title.Trim();
        Instructions = instructions.Trim();
        ActivityType = activityType.Trim();
        RendererType = rendererType;
        SourceMode = sourceMode;
        Description = description?.Trim();
        PatternKey = patternKey?.Trim();
        FormSchemaJson = formSchemaJson;
        AnswerKeyJson = answerKeyJson;
        ScoringRulesJson = scoringRulesJson;
        FeedbackPlanJson = feedbackPlanJson;
        CefrLevel = cefrLevel?.Trim().ToUpperInvariant();
        Skill = skill?.Trim();
        Subskill = subskill?.Trim();
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        DifficultyBand = difficultyBand;
        EstimatedMinutes = estimatedMinutes;
        LessonId = lessonId;
        GenerationProvider = generationProvider?.Trim();
        GenerationModel = generationModel?.Trim();
        CreatedByUserId = createdByUserId;
        ReviewStatus = AdminReviewStatus.PendingReview;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Edits draft content/metadata. Blocked once approved — same policy as
    /// <see cref="Lesson.UpdateDraft"/>: reject first to reopen editing. Editing a rejected
    /// draft resubmits it (moves <see cref="ReviewStatus"/> back to
    /// <see cref="AdminReviewStatus.PendingReview"/>).</summary>
    public void UpdateDraft(
        string title,
        string instructions,
        string? description,
        string? formSchemaJson,
        string? answerKeyJson,
        string? scoringRulesJson,
        string? feedbackPlanJson,
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
                $"Cannot edit Activity '{Title}': it is already approved. Reject it first to reopen editing.");

        ValidateAuthorableFields(title, instructions, ActivityType, cefrLevel, difficultyBand, estimatedMinutes);

        Title = title.Trim();
        Instructions = instructions.Trim();
        Description = description?.Trim();
        FormSchemaJson = formSchemaJson;
        AnswerKeyJson = answerKeyJson;
        ScoringRulesJson = scoringRulesJson;
        FeedbackPlanJson = feedbackPlanJson;
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
            throw new ArgumentException("Reason is required to reject an Activity.", nameof(reason));

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
        string title, string instructions, string activityType,
        string? cefrLevel, int? difficultyBand, int? estimatedMinutes)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("Instructions is required.", nameof(instructions));
        if (string.IsNullOrWhiteSpace(activityType))
            throw new ArgumentException("ActivityType is required.", nameof(activityType));
        if (cefrLevel is not null && !CefrLevelConstants.IsValid(cefrLevel))
            throw new ArgumentException($"Invalid CEFR level '{cefrLevel}'. Must be one of: {string.Join(", ", CefrLevelConstants.All)}.", nameof(cefrLevel));
        if (difficultyBand is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(difficultyBand), "DifficultyBand must be between 1 and 5.");
        if (estimatedMinutes is < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedMinutes), "EstimatedMinutes must be >= 0.");
    }
}
