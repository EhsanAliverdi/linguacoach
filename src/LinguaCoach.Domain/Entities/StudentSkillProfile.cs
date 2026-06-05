using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

public sealed class StudentSkillProfile : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public string SkillKey { get; private set; }
    public string SkillLabel { get; private set; }
    public bool IsWeak { get; private set; }
    public DateTime LastUpdatedUtc { get; private set; }

    private StudentSkillProfile()
    {
        SkillKey = string.Empty;
        SkillLabel = string.Empty;
    }

    public StudentSkillProfile(Guid studentProfileId, string skillKey, string skillLabel, bool isWeak)
    {
        if (studentProfileId == Guid.Empty) throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(skillKey)) throw new ArgumentException("Skill key is required.", nameof(skillKey));
        if (string.IsNullOrWhiteSpace(skillLabel)) throw new ArgumentException("Skill label is required.", nameof(skillLabel));

        StudentProfileId = studentProfileId;
        SkillKey = NormaliseSkillKey(skillKey);
        SkillLabel = skillLabel.Trim();
        IsWeak = isWeak;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void MarkWeak(bool isWeak)
    {
        IsWeak = isWeak;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public static string NormaliseSkillKey(string value)
        => value.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
}
