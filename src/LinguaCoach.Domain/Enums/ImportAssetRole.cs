namespace LinguaCoach.Domain.Enums;

/// <summary>Phase 4 — the role an <see cref="Entities.ImportAsset"/> plays within its package.
/// AI may suggest a role during package inspection/sample analysis; an admin may correct it.</summary>
public enum ImportAssetRole
{
    Unknown = 0,
    PrimaryContent = 1,
    Audio = 2,
    Transcript = 3,
    Image = 4,
    Video = 5,
    Instructions = 6,
    AnswerKey = 7,
    Metadata = 8,
    Mapping = 9,
    Licence = 10,
    Reference = 11,
    ModelAnswer = 12,
    SupportingContent = 13
}
