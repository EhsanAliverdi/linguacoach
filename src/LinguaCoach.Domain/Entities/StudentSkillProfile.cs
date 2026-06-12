using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

public sealed class StudentSkillProfile : BaseEntity
{
    public const int DefaultScorePercent = 50;
    public const int WeakThreshold = 50;

    public Guid StudentProfileId { get; private set; }
    public string SkillKey { get; private set; }
    public string SkillLabel { get; private set; }
    public int ScorePercent { get; private set; }
    public DateTime LastUpdatedUtc { get; private set; }

    public bool IsWeak => ScorePercent < WeakThreshold;

    private StudentSkillProfile()
    {
        SkillKey = string.Empty;
        SkillLabel = string.Empty;
    }

    public StudentSkillProfile(Guid studentProfileId, string skillKey, string skillLabel, bool isWeak)
        : this(studentProfileId, skillKey, skillLabel, isWeak ? WeakThreshold - 10 : WeakThreshold + 10)
    {
    }

    public StudentSkillProfile(Guid studentProfileId, string skillKey, string skillLabel, int scorePercent)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(skillKey)) throw new ArgumentException("Skill key is required.", nameof(skillKey));
        if (string.IsNullOrWhiteSpace(skillLabel)) throw new ArgumentException("Skill label is required.", nameof(skillLabel));

        StudentProfileId = studentProfileId;
        SkillKey = NormaliseSkillKey(skillKey);
        SkillLabel = skillLabel.Trim();
        ScorePercent = Math.Clamp(scorePercent, 0, 100);
        LastUpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the skill as weak or strong by snapping the score to the nearer side of the
    /// weak threshold. Kept for callers that only know a boolean, not a magnitude.
    /// </summary>
    public void MarkWeak(bool isWeak)
    {
        ScorePercent = isWeak ? WeakThreshold - 10 : WeakThreshold + 10;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Applies a signed delta (e.g. derived from a -1..1 evaluation impact) to the
    /// running score, clamped to 0-100.
    /// </summary>
    public void ApplyScoreDelta(int delta)
    {
        ScorePercent = Math.Clamp(ScorePercent + delta, 0, 100);
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public static string NormaliseSkillKey(string value)
        => value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
}
