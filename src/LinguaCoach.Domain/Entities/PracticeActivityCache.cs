using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A pre-generated Practice Gym activity held ready for a student + pattern + level + domain.
/// Once assigned, the activity must not change on page refresh.
/// </summary>
public sealed class PracticeActivityCache : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public string PatternKey { get; private set; }
    public string CefrLevel { get; private set; }
    public string DomainComplexity { get; private set; }
    public string? SkillFocus { get; private set; }

    /// <summary>Duplicate-prevention fingerprint for generated content.</summary>
    public string ContentFingerprint { get; private set; }

    public Guid? LearningActivityId { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public PracticeCacheStatus Status { get; private set; }

    private PracticeActivityCache()
    {
        PatternKey = string.Empty;
        CefrLevel = string.Empty;
        DomainComplexity = string.Empty;
        ContentFingerprint = string.Empty;
    }

    public PracticeActivityCache(
        Guid studentProfileId,
        string patternKey,
        string cefrLevel,
        string domainComplexity,
        string contentFingerprint,
        string? skillFocus = null,
        Guid? learningActivityId = null,
        DateTime? expiresAtUtc = null,
        PracticeCacheStatus status = PracticeCacheStatus.Pending)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(patternKey))
            throw new ArgumentException("PatternKey is required.", nameof(patternKey));
        if (string.IsNullOrWhiteSpace(cefrLevel))
            throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(domainComplexity))
            throw new ArgumentException("DomainComplexity is required.", nameof(domainComplexity));

        StudentProfileId = studentProfileId;
        PatternKey = patternKey.Trim();
        CefrLevel = cefrLevel.Trim();
        DomainComplexity = domainComplexity.Trim();
        ContentFingerprint = string.IsNullOrWhiteSpace(contentFingerprint) ? Guid.NewGuid().ToString("N") : contentFingerprint.Trim();
        SkillFocus = skillFocus;
        LearningActivityId = learningActivityId;
        ExpiresAtUtc = expiresAtUtc;
        Status = status;
    }

    public void MarkReady(Guid learningActivityId)
    {
        LearningActivityId = learningActivityId;
        Status = PracticeCacheStatus.Ready;
    }

    public void MarkAssigned() => Status = PracticeCacheStatus.Assigned;
    public void MarkCompleted() => Status = PracticeCacheStatus.Completed;
    public void MarkExpired() => Status = PracticeCacheStatus.Expired;
}
