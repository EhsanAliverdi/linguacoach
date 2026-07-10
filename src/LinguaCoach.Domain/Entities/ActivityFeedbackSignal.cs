using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records a student's self-reported feedback on a completed activity — difficulty, clarity,
/// usefulness, and repeat preference — for both Today lesson and Practice Gym completions.
/// See docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md (Phase B2).
///
/// Idempotent per (StudentProfileId, ActivityAttemptId) when ActivityAttemptId is known, else
/// per (StudentProfileId, LearningActivityId) — resubmitting for the same key updates the
/// existing row (<see cref="UpdateRatings"/>) rather than creating a duplicate. Unlike
/// <see cref="StudentActivityUsageLog"/> (append-only), this entity is mutable/upsertable.
/// </summary>
public sealed class ActivityFeedbackSignal : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public Guid LearningActivityId { get; private set; }
    public Guid? ActivityAttemptId { get; private set; }
    public Guid? StudentActivityUsageLogId { get; private set; }
    public Guid? SourceTemplateId { get; private set; }
    public Guid? SourceBankItemId { get; private set; }

    // --- Classification (denormalized snapshot, mirrors StudentActivityUsageLog) ---
    public string? PatternKey { get; private set; }
    public string? Skill { get; private set; }
    public string? Subskill { get; private set; }
    public string? CefrLevel { get; private set; }
    public string? CurriculumObjectiveKey { get; private set; }

    // --- Student-reported ratings ---
    public ActivityFeedbackDifficultyRating DifficultyRating { get; private set; }
    public ActivityFeedbackClarityRating ClarityRating { get; private set; }
    public ActivityFeedbackUsefulnessRating UsefulnessRating { get; private set; }
    public ActivityFeedbackRepeatPreference RepeatPreference { get; private set; }
    public string? OptionalComment { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public const int MaxOptionalCommentLength = 500;

    private ActivityFeedbackSignal() { }

    public ActivityFeedbackSignal(
        Guid studentProfileId,
        Guid learningActivityId,
        ActivityFeedbackDifficultyRating difficultyRating,
        ActivityFeedbackClarityRating clarityRating,
        ActivityFeedbackUsefulnessRating usefulnessRating,
        ActivityFeedbackRepeatPreference repeatPreference,
        Guid? activityAttemptId = null,
        Guid? studentActivityUsageLogId = null,
        Guid? sourceTemplateId = null,
        Guid? sourceBankItemId = null,
        string? patternKey = null,
        string? skill = null,
        string? subskill = null,
        string? cefrLevel = null,
        string? curriculumObjectiveKey = null,
        string? optionalComment = null)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (learningActivityId == Guid.Empty)
            throw new ArgumentException("LearningActivityId must not be empty.", nameof(learningActivityId));
        ValidateComment(optionalComment);

        StudentProfileId = studentProfileId;
        LearningActivityId = learningActivityId;
        ActivityAttemptId = activityAttemptId;
        StudentActivityUsageLogId = studentActivityUsageLogId;
        SourceTemplateId = sourceTemplateId;
        SourceBankItemId = sourceBankItemId;
        PatternKey = patternKey?.Trim();
        Skill = skill?.Trim().ToLowerInvariant();
        Subskill = subskill?.Trim().ToLowerInvariant();
        CefrLevel = cefrLevel?.Trim();
        CurriculumObjectiveKey = curriculumObjectiveKey?.Trim();
        DifficultyRating = difficultyRating;
        ClarityRating = clarityRating;
        UsefulnessRating = usefulnessRating;
        RepeatPreference = repeatPreference;
        OptionalComment = optionalComment?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Upsert path: a resubmission for the same idempotency key updates ratings and
    /// provenance backfill in place rather than creating a duplicate row.</summary>
    public void UpdateRatings(
        ActivityFeedbackDifficultyRating difficultyRating,
        ActivityFeedbackClarityRating clarityRating,
        ActivityFeedbackUsefulnessRating usefulnessRating,
        ActivityFeedbackRepeatPreference repeatPreference,
        string? optionalComment,
        Guid? studentActivityUsageLogId,
        Guid? sourceTemplateId,
        Guid? sourceBankItemId,
        string? patternKey,
        string? skill,
        string? subskill,
        string? cefrLevel,
        string? curriculumObjectiveKey)
    {
        ValidateComment(optionalComment);

        DifficultyRating = difficultyRating;
        ClarityRating = clarityRating;
        UsefulnessRating = usefulnessRating;
        RepeatPreference = repeatPreference;
        OptionalComment = optionalComment?.Trim();

        // Backfill provenance only when not already known — never overwrite a known value with null.
        StudentActivityUsageLogId ??= studentActivityUsageLogId;
        SourceTemplateId ??= sourceTemplateId;
        SourceBankItemId ??= sourceBankItemId;
        PatternKey ??= patternKey?.Trim();
        Skill ??= skill?.Trim().ToLowerInvariant();
        Subskill ??= subskill?.Trim().ToLowerInvariant();
        CefrLevel ??= cefrLevel?.Trim();
        CurriculumObjectiveKey ??= curriculumObjectiveKey?.Trim();

        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateComment(string? optionalComment)
    {
        if (optionalComment is not null && optionalComment.Length > MaxOptionalCommentLength)
            throw new ArgumentException(
                $"OptionalComment must be at most {MaxOptionalCommentLength} characters.", nameof(optionalComment));
    }
}
