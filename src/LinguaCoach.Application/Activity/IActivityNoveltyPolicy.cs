namespace LinguaCoach.Application.Activity;

public enum NoveltyBlockReason
{
    None,
    SameFingerprintTooRecent,
    SameTemplateTooRecent,
    SameTopicTooRecent,
    SameScenarioTooRecent
}

/// <summary>
/// Request to check whether a piece of activity content is allowed to be served to a student
/// right now, per the Phase B repetition/novelty foundation (deterministic fingerprint +
/// cooldown only — see docs/architecture/repetition-and-novelty.md).
/// </summary>
public sealed record ActivityNoveltyCheckRequest(
    Guid StudentProfileId,
    string ContentFingerprint,
    Guid? SourceTemplateId = null,
    string? TopicKey = null,
    string? ScenarioKey = null,
    /// <summary>True when this is a deliberate spaced-review/remediation repeat (e.g.
    /// RoutingReason.Review/Scaffold/Remediation) — bypasses all cooldowns when true.</summary>
    bool IsIntentionalReview = false,
    DateTime? NowUtc = null);

public sealed record ActivityNoveltyResult(
    bool Allowed,
    NoveltyBlockReason Reason,
    Guid? BlockingUsageLogId = null,
    DateTime? CooldownUntilUtc = null,
    string? MatchedFingerprint = null,
    Guid? MatchedTemplateId = null,
    string? MatchedTopicKey = null)
{
    public static ActivityNoveltyResult Allow() => new(Allowed: true, Reason: NoveltyBlockReason.None);
}

/// <summary>
/// Answers "is this activity allowed for this student now?" using the student's
/// <c>StudentActivityUsageLog</c> history. Exact/deterministic cooldown checks only — no
/// embeddings, no semantic near-duplicate detection (see Phase B scope notes in
/// docs/architecture/repetition-and-novelty.md).
/// </summary>
public interface IActivityNoveltyPolicy
{
    Task<ActivityNoveltyResult> CheckAsync(ActivityNoveltyCheckRequest request, CancellationToken ct = default);
}
