namespace LinguaCoach.Infrastructure.Activity;

/// <summary>
/// Safe code-level defaults for the Phase B novelty/cooldown policy. Optionally overridable via
/// the "Novelty" configuration section. No admin UI in this phase — see
/// docs/architecture/repetition-and-novelty.md.
/// </summary>
public sealed class NoveltyPolicySettings
{
    /// <summary>Exact-content-fingerprint match is blocked for this many days. Long by design —
    /// truly identical generated content repeating is close to a generation bug, not an
    /// acceptable "review" repeat (use IsIntentionalReview for deliberate repeats instead).</summary>
    public int FingerprintCooldownDays { get; set; } = 60;

    /// <summary>Same ActivityTemplate (bank-first path) is blocked for this many days, unless
    /// the request is an intentional review. Shorter than the fingerprint cooldown because
    /// template reuse with fresh AI personalization is expected once the template bank is
    /// small.</summary>
    public int TemplateCooldownDays { get; set; } = 3;

    /// <summary>Same TopicKey is blocked for this many days, unless intentional review.</summary>
    public int TopicCooldownDays { get; set; } = 7;

    /// <summary>Same ScenarioKey is blocked for this many days, unless intentional review.</summary>
    public int ScenarioCooldownDays { get; set; } = 7;
}
