using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/*
 * Lifecycle:
 *   queued → generating → ready → reserved → consumed  (terminal)
 *                      ↓            ↓
 *                   failed        expired               (terminal unless re-created)
 *   ready/reserved → stale                             (do not serve as normal)
 *   ready/reserved → review_only                       (serve only for review queries)
 *   ready/reserved/review_only → skipped               (mastered or irrelevant — terminal)
 *   reserved → expired  (reservation too old or item expired)
 *
 *   IsLowerLevelContent=true requires RoutingReason != Normal.
 *   general_english is the default/fallback context — not workplace.
 *   LastEvaluatedAtUtc: set whenever the item is evaluated for stale/profile-match.
 */

/// <summary>
/// Tracks a single prepared or in-progress learning item for a student's readiness pool.
/// Preserves a routing/personalisation snapshot so content is not silently mis-matched
/// if student profile preferences change after generation.
/// </summary>
public sealed class StudentActivityReadinessItem : BaseEntity
{
    public Guid StudentId { get; private set; }
    public ReadinessPoolSource Source { get; private set; }
    public ReadinessPoolStatus Status { get; private set; }

    /// <summary>Lower = higher priority. Defaults to 0 (highest).</summary>
    public int Priority { get; private set; }

    // --- Routing snapshot ---
    public string TargetCefrLevel { get; private set; }
    public string? OriginalCefrLevelSnapshot { get; private set; }

    /// <summary>True when this content is below the student's current CEFR level.</summary>
    public bool IsLowerLevelContent { get; private set; }

    public RoutingReason RoutingReason { get; private set; }
    public string? RoutingExplanation { get; private set; }

    public string? CurriculumObjectiveKey { get; private set; }
    public string? CurriculumObjectiveTitle { get; private set; }

    public string? PrimarySkill { get; private set; }

    /// <summary>JSON array of secondary skill strings.</summary>
    public string SecondarySkillsJson { get; private set; }

    /// <summary>JSON array of context tag strings (e.g. ["general_english"]).</summary>
    public string ContextTagsJson { get; private set; }

    /// <summary>JSON array of focus area tag strings.</summary>
    public string FocusTagsJson { get; private set; }

    /// <summary>Exercise pattern key if this item targets a specific pattern.</summary>
    public string? PatternKey { get; private set; }

    public string? ActivityType { get; private set; }

    public int DifficultyBand { get; private set; }

    /// <summary>
    /// True when this item was generated under RequireAdminReview=true. Excluded from
    /// Practice Gym suggestion buckets until an admin flips the global config flag off.
    /// A creation-time config snapshot, not a mutable per-item approval state.
    /// </summary>
    public bool RequiresAdminReview { get; private set; }

    // --- Admin approval (Phase 19B, per-item) ---
    public AdminReviewStatus AdminReviewStatus { get; private set; }
    public DateTime? AdminReviewedAtUtc { get; private set; }
    public Guid? AdminReviewedByUserId { get; private set; }
    public string? AdminReviewReason { get; private set; }
    public string? AdminReviewNotes { get; private set; }

    // --- Preference snapshot ---
    public int? PreferredSessionDurationMinutes { get; private set; }
    public string? DifficultyPreference { get; private set; }
    public string? SupportLanguageCode { get; private set; }
    public string? SupportLanguageName { get; private set; }
    public string? TranslationHelpPreference { get; private set; }

    // --- Linked entities (set when materialized) ---
    public Guid? LearningSessionId { get; private set; }
    public Guid? LearningActivityId { get; private set; }
    public Guid? SessionExerciseId { get; private set; }

    // --- Generation provenance ---
    public string? GeneratedBy { get; private set; }

    // --- Error info ---
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int AttemptCount { get; private set; }

    // --- Lifecycle timestamps ---
    public DateTime? ReservedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? StaleAt { get; private set; }
    /// <summary>UTC time the item was last checked for staleness/profile-match by the replenishment sweep.</summary>
    public DateTime? LastEvaluatedAtUtc { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private StudentActivityReadinessItem()
    {
        TargetCefrLevel = string.Empty;
        SecondarySkillsJson = "[]";
        ContextTagsJson = "[]";
        FocusTagsJson = "[]";
    }

    public StudentActivityReadinessItem(
        Guid studentId,
        ReadinessPoolSource source,
        string targetCefrLevel,
        RoutingReason routingReason,
        bool isLowerLevelContent,
        string? curriculumObjectiveKey = null,
        string? curriculumObjectiveTitle = null,
        string? primarySkill = null,
        string secondarySkillsJson = "[]",
        string contextTagsJson = "[]",
        string focusTagsJson = "[]",
        string? patternKey = null,
        string? activityType = null,
        int difficultyBand = 1,
        string? originalCefrLevelSnapshot = null,
        string? routingExplanation = null,
        int? preferredSessionDurationMinutes = null,
        string? difficultyPreference = null,
        string? supportLanguageCode = null,
        string? supportLanguageName = null,
        string? translationHelpPreference = null,
        string? generatedBy = null,
        int priority = 0,
        DateTime? expiresAt = null,
        bool requiresAdminReview = false)
    {
        if (studentId == Guid.Empty)
            throw new ArgumentException("StudentId is required.", nameof(studentId));
        if (string.IsNullOrWhiteSpace(targetCefrLevel))
            throw new ArgumentException("TargetCefrLevel is required.", nameof(targetCefrLevel));
        if (isLowerLevelContent && routingReason == RoutingReason.Normal)
            throw new ArgumentException(
                "IsLowerLevelContent=true requires a non-Normal RoutingReason (review/scaffold/remediation/fallback).",
                nameof(routingReason));

        StudentId = studentId;
        Source = source;
        Status = ReadinessPoolStatus.Queued;
        Priority = priority;
        TargetCefrLevel = targetCefrLevel.ToUpperInvariant().Trim();
        OriginalCefrLevelSnapshot = originalCefrLevelSnapshot?.ToUpperInvariant().Trim();
        IsLowerLevelContent = isLowerLevelContent;
        RoutingReason = routingReason;
        RoutingExplanation = routingExplanation?.Trim();
        CurriculumObjectiveKey = curriculumObjectiveKey?.Trim();
        CurriculumObjectiveTitle = curriculumObjectiveTitle?.Trim();
        PrimarySkill = primarySkill?.ToLowerInvariant().Trim();
        SecondarySkillsJson = string.IsNullOrWhiteSpace(secondarySkillsJson) ? "[]" : secondarySkillsJson;
        ContextTagsJson = string.IsNullOrWhiteSpace(contextTagsJson) ? "[]" : contextTagsJson;
        FocusTagsJson = string.IsNullOrWhiteSpace(focusTagsJson) ? "[]" : focusTagsJson;
        PatternKey = patternKey?.Trim();
        ActivityType = activityType?.Trim();
        DifficultyBand = Math.Max(1, Math.Min(5, difficultyBand));
        PreferredSessionDurationMinutes = preferredSessionDurationMinutes;
        DifficultyPreference = difficultyPreference?.Trim();
        SupportLanguageCode = supportLanguageCode?.Trim();
        SupportLanguageName = supportLanguageName?.Trim();
        TranslationHelpPreference = translationHelpPreference?.Trim();
        GeneratedBy = generatedBy?.Trim();
        AttemptCount = 0;
        ExpiresAt = expiresAt;
        RequiresAdminReview = requiresAdminReview;
        AdminReviewStatus = requiresAdminReview ? AdminReviewStatus.PendingReview : AdminReviewStatus.NotRequired;
        UpdatedAt = DateTime.UtcNow;
    }

    // --- Lifecycle transitions ---

    public void MarkGenerating()
    {
        EnsureStatus(ReadinessPoolStatus.Queued, "MarkGenerating");
        Status = ReadinessPoolStatus.Generating;
        AttemptCount++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReady(
        Guid? learningSessionId = null,
        Guid? learningActivityId = null,
        Guid? sessionExerciseId = null)
    {
        EnsureStatus(ReadinessPoolStatus.Generating, "MarkReady");
        Status = ReadinessPoolStatus.Ready;
        LearningSessionId = learningSessionId;
        LearningActivityId = learningActivityId;
        SessionExerciseId = sessionExerciseId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string? errorCode, string? errorMessage)
    {
        EnsureStatus(ReadinessPoolStatus.Generating, "MarkFailed");
        Status = ReadinessPoolStatus.Failed;
        ErrorCode = errorCode?.Trim();
        ErrorMessage = errorMessage?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reserve()
    {
        EnsureStatus(ReadinessPoolStatus.Ready, "Reserve");
        Status = ReadinessPoolStatus.Reserved;
        ReservedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkConsumed()
    {
        EnsureStatus(ReadinessPoolStatus.Reserved, "MarkConsumed");
        Status = ReadinessPoolStatus.Consumed;
        ConsumedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Expire(string? reason = null)
    {
        if (Status is ReadinessPoolStatus.Consumed or ReadinessPoolStatus.Expired)
            throw new InvalidOperationException(
                $"Cannot expire item {Id} in terminal status {Status}.");
        Status = ReadinessPoolStatus.Expired;
        if (reason is not null)
            ErrorMessage = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkStale(string? reason = null)
    {
        if (Status is not (ReadinessPoolStatus.Ready or ReadinessPoolStatus.Reserved))
            throw new InvalidOperationException(
                $"MarkStale requires Ready or Reserved status. Current: {Status}.");
        Status = ReadinessPoolStatus.Stale;
        StaleAt = DateTime.UtcNow;
        if (reason is not null)
            ErrorMessage = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkReviewOnly(string? reason = null)
    {
        if (Status is not (ReadinessPoolStatus.Ready or ReadinessPoolStatus.Reserved))
            throw new InvalidOperationException(
                $"MarkReviewOnly requires Ready or Reserved status. Current: {Status}.");
        Status = ReadinessPoolStatus.ReviewOnly;
        if (reason is not null)
            ErrorMessage = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks this item as intentionally skipped — mastery achieved or content no longer
    /// relevant even for review. Terminal (same as Expired).
    /// Valid from: Ready, Reserved, ReviewOnly.
    /// </summary>
    public void MarkSkipped(string? reason = null)
    {
        if (Status is ReadinessPoolStatus.Consumed
                    or ReadinessPoolStatus.Expired
                    or ReadinessPoolStatus.Skipped)
            throw new InvalidOperationException(
                $"Cannot skip item {Id} in terminal status {Status}.");
        Status = ReadinessPoolStatus.Skipped;
        if (reason is not null)
            ErrorMessage = reason.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Records that the replenishment engine has evaluated this item for staleness/profile-match.</summary>
    public void RecordEvaluation()
    {
        LastEvaluatedAtUtc = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LinkMaterializedIds(
        Guid? learningSessionId,
        Guid? learningActivityId,
        Guid? sessionExerciseId)
    {
        LearningSessionId = learningSessionId ?? LearningSessionId;
        LearningActivityId = learningActivityId ?? LearningActivityId;
        SessionExerciseId = sessionExerciseId ?? SessionExerciseId;
        UpdatedAt = DateTime.UtcNow;
    }

    // --- Admin approval transitions (Phase 19B) ---

    /// <summary>Statuses in which an admin decision may still be applied (not terminal/in-flight).</summary>
    private static bool IsReviewableLifecycleStatus(ReadinessPoolStatus status) =>
        status is ReadinessPoolStatus.Ready or ReadinessPoolStatus.ReviewOnly or ReadinessPoolStatus.Reserved;

    /// <summary>
    /// Approves a pending review-scaffold item. Idempotent if already Approved.
    /// Never mutates CEFR, objective completion, or the Learning Plan.
    /// </summary>
    public void ApproveAdminReview(Guid adminUserId, string? notes = null)
    {
        if (AdminReviewStatus == AdminReviewStatus.Approved)
            return; // idempotent no-op

        if (AdminReviewStatus != AdminReviewStatus.PendingReview)
            throw new InvalidOperationException(
                $"ApproveAdminReview requires AdminReviewStatus=PendingReview. Current: {AdminReviewStatus}. Item: {Id}.");

        if (!IsReviewableLifecycleStatus(Status))
            throw new InvalidOperationException(
                $"Cannot approve item {Id} with lifecycle status {Status} (expired/failed/stale/consumed/skipped items are not approvable).");

        AdminReviewStatus = AdminReviewStatus.Approved;
        AdminReviewedAtUtc = DateTime.UtcNow;
        AdminReviewedByUserId = adminUserId;
        AdminReviewReason = null;
        AdminReviewNotes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rejects a review-scaffold item. Idempotent if already Rejected. Allowed from PendingReview,
    /// or from Approved as long as the item has not been consumed.
    /// </summary>
    public void RejectAdminReview(Guid adminUserId, string reason, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required to reject a review scaffold item.", nameof(reason));

        if (AdminReviewStatus == AdminReviewStatus.Rejected)
            return; // idempotent no-op

        if (AdminReviewStatus == AdminReviewStatus.NotRequired)
            throw new InvalidOperationException(
                $"Cannot reject item {Id}: it does not belong to the review scaffold flow (AdminReviewStatus=NotRequired).");

        if (AdminReviewStatus == AdminReviewStatus.Approved && Status == ReadinessPoolStatus.Consumed)
            throw new InvalidOperationException(
                $"Cannot reject item {Id}: it has already been consumed.");

        AdminReviewStatus = AdminReviewStatus.Rejected;
        AdminReviewedAtUtc = DateTime.UtcNow;
        AdminReviewedByUserId = adminUserId;
        AdminReviewReason = reason.Trim();
        AdminReviewNotes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reopens a rejected item back to PendingReview. Idempotent if already PendingReview.
    /// Not allowed once the item has been consumed.
    /// </summary>
    public void ReopenAdminReview(Guid adminUserId, string? notes = null)
    {
        if (AdminReviewStatus == AdminReviewStatus.PendingReview)
            return; // idempotent no-op

        if (AdminReviewStatus != AdminReviewStatus.Rejected)
            throw new InvalidOperationException(
                $"ReopenAdminReview requires AdminReviewStatus=Rejected. Current: {AdminReviewStatus}. Item: {Id}.");

        if (Status == ReadinessPoolStatus.Consumed)
            throw new InvalidOperationException($"Cannot reopen item {Id}: it has already been consumed.");

        AdminReviewStatus = AdminReviewStatus.PendingReview;
        AdminReviewedAtUtc = DateTime.UtcNow;
        AdminReviewedByUserId = adminUserId;
        AdminReviewReason = null;
        AdminReviewNotes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>True when a student could currently be served this item (approval gate only; other lifecycle gates apply separately).</summary>
    public bool PassesAdminReviewGate =>
        AdminReviewStatus is AdminReviewStatus.NotRequired or AdminReviewStatus.Approved;

    public bool IsServableAsNormalContent =>
        Status == ReadinessPoolStatus.Ready;

    public bool IsServableAsReview =>
        Status is ReadinessPoolStatus.Ready or ReadinessPoolStatus.ReviewOnly;

    private void EnsureStatus(ReadinessPoolStatus expected, string operation)
    {
        if (Status != expected)
            throw new InvalidOperationException(
                $"{operation} requires status {expected}. Current: {Status}. Item: {Id}.");
    }
}
