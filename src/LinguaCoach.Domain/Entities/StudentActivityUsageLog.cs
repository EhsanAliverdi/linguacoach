using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Records that a student actually consumed (completed) a piece of activity content, with
/// enough content-identifying metadata to detect and prevent repetition later. This is the
/// real content-usage history that the various pattern-key/topic prompt hints
/// (<c>DynamicPatternSelector</c>, <c>LessonBatchGenerationJob</c>'s
/// avoidRepeating/coveredScenarios) do NOT provide — see
/// docs/architecture/repetition-and-novelty.md and
/// docs/reviews/2026-07-08-bank-first-ai-teaching-clean-architecture-plan.md (Phase B). The
/// Practice Gym pre-generation queue-slot fingerprint this used to be contrasted against
/// (<c>PracticeActivityCache.ContentFingerprint</c>) was removed in Phase I2A; see
/// docs/reviews/2026-07-10-phase-i2a-practice-gym-legacy-deletion-review.md.
///
/// Append-only — never mutated after creation. One row per real consumption event, not per
/// generation attempt.
/// </summary>
public sealed class StudentActivityUsageLog : BaseEntity
{
    public Guid StudentProfileId { get; private set; }

    // --- What was consumed ---
    public Guid? LearningActivityId { get; private set; }
    public Guid? StudentActivityReadinessItemId { get; private set; }
    public Guid? SourceTemplateId { get; private set; }
    public Guid? SourceBankItemId { get; private set; }

    // --- Classification (denormalized snapshot at consumption time) ---
    public string? PatternKey { get; private set; }
    public string? ActivityType { get; private set; }
    public string? Skill { get; private set; }
    public string? Subskill { get; private set; }
    public string? CefrLevel { get; private set; }
    public string? CurriculumObjectiveKey { get; private set; }

    // --- Content identity ---
    /// <summary>Deterministic content fingerprint from <c>IActivityContentFingerprintService</c>.
    /// Distinct from the now-removed <c>PracticeActivityCache.ContentFingerprint</c> (Phase I2A),
    /// which was a queue-slot uniqueness key computed before any content existed.</summary>
    public string ContentFingerprint { get; private set; }

    public string? TopicKey { get; private set; }
    public string? ScenarioKey { get; private set; }
    public string? PassageKey { get; private set; }
    public string? PromptKey { get; private set; }
    public string? ContextTagsJson { get; private set; }
    public string? FocusTagsJson { get; private set; }

    // --- Intentional-repeat labelling ---
    /// <summary>True when this consumption is a deliberate spaced-review/remediation repeat
    /// (e.g. RoutingReason.Review/Scaffold/Remediation), so novelty checks should not treat it
    /// as an accidental duplicate.</summary>
    public bool IsIntentionalReview { get; private set; }
    public string? ReviewReason { get; private set; }

    public DateTime ConsumedAtUtc { get; private set; }

    private StudentActivityUsageLog()
    {
        ContentFingerprint = string.Empty;
    }

    public StudentActivityUsageLog(
        Guid studentProfileId,
        string contentFingerprint,
        DateTime consumedAtUtc,
        Guid? learningActivityId = null,
        Guid? studentActivityReadinessItemId = null,
        Guid? sourceTemplateId = null,
        Guid? sourceBankItemId = null,
        string? patternKey = null,
        string? activityType = null,
        string? skill = null,
        string? subskill = null,
        string? cefrLevel = null,
        string? curriculumObjectiveKey = null,
        string? topicKey = null,
        string? scenarioKey = null,
        string? passageKey = null,
        string? promptKey = null,
        string? contextTagsJson = null,
        string? focusTagsJson = null,
        bool isIntentionalReview = false,
        string? reviewReason = null)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(contentFingerprint))
            throw new ArgumentException("ContentFingerprint is required.", nameof(contentFingerprint));

        StudentProfileId = studentProfileId;
        ContentFingerprint = contentFingerprint.Trim();
        ConsumedAtUtc = consumedAtUtc;
        LearningActivityId = learningActivityId;
        StudentActivityReadinessItemId = studentActivityReadinessItemId;
        SourceTemplateId = sourceTemplateId;
        SourceBankItemId = sourceBankItemId;
        PatternKey = patternKey?.Trim();
        ActivityType = activityType?.Trim();
        Skill = skill?.Trim().ToLowerInvariant();
        Subskill = subskill?.Trim().ToLowerInvariant();
        CefrLevel = cefrLevel?.Trim();
        CurriculumObjectiveKey = curriculumObjectiveKey?.Trim();
        TopicKey = topicKey?.Trim();
        ScenarioKey = scenarioKey?.Trim();
        PassageKey = passageKey?.Trim();
        PromptKey = promptKey?.Trim();
        ContextTagsJson = contextTagsJson;
        FocusTagsJson = focusTagsJson;
        IsIntentionalReview = isIntentionalReview;
        ReviewReason = reviewReason?.Trim();
    }
}
