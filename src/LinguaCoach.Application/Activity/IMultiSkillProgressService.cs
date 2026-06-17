using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Application.Activity;

// ── Request ───────────────────────────────────────────────────────────────────

/// <summary>
/// Input for a multi-skill progress update after any activity attempt.
/// Skill information is derived from pattern/activity/curriculum metadata in priority order:
///   1. ExercisePatternDefinition.PrimarySkill + SecondarySkillsJson
///   2. LearningActivity.ActivityType fallback
///   3. Caller-supplied overrides
/// </summary>
public sealed record MultiSkillProgressUpdateRequest(
    Guid StudentProfileId,
    /// <summary>Normalised primary skill key (e.g. "writing", "listening").</summary>
    string PrimarySkill,
    /// <summary>Normalised secondary skill keys. Duplicates are de-duplicated internally.</summary>
    IReadOnlyList<string> SecondarySkills,
    /// <summary>0–100 percentage score for the attempt.</summary>
    double NormalizedScore,
    /// <summary>Whether the attempt counts as completed (not just submitted).</summary>
    bool Completed,
    /// <summary>Source tag written to log messages and ledger metadata.</summary>
    string Source,
    /// <summary>True when this activity is lower-level review/scaffold content.</summary>
    bool IsLowerLevelContent = false,
    /// <summary>Optional routing reason recorded in metadata.</summary>
    string? RoutingReason = null);

// ── Result ────────────────────────────────────────────────────────────────────

public sealed record MultiSkillProgressUpdateResult(
    /// <summary>All skills that were updated (primary + secondary).</summary>
    IReadOnlyList<string> UpdatedSkills,
    /// <summary>Score delta applied to each updated skill key.</summary>
    IReadOnlyDictionary<string, int> ScoreDeltaBySkill,
    /// <summary>Human-readable notes for debugging/logging.</summary>
    string Notes);

// ── Interface ─────────────────────────────────────────────────────────────────

public interface IMultiSkillProgressService
{
    /// <summary>
    /// Updates StudentSkillProfile rows for all affected skills.
    /// Best-effort: swallows exceptions and returns a result with Notes describing the failure.
    /// Never throws.
    /// </summary>
    Task<MultiSkillProgressUpdateResult> ApplyAsync(
        MultiSkillProgressUpdateRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Derives a MultiSkillProgressUpdateRequest from exercise pattern metadata when available,
    /// falling back to ActivityType when pattern metadata is absent.
    /// </summary>
    MultiSkillProgressUpdateRequest BuildRequest(
        Guid studentProfileId,
        string? exercisePatternKey,
        string? patternPrimarySkill,
        IReadOnlyList<string>? patternSecondarySkills,
        ActivityType activityType,
        double normalizedScore,
        bool completed,
        string source,
        bool isLowerLevelContent = false,
        string? routingReason = null);
}
