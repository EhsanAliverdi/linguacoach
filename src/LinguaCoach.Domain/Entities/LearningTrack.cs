using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A named learning track scoped to a language pair.
/// Seed: "Workplace English" for the Persian→English pair.
/// </summary>
public sealed class LearningTrack : BaseEntity
{
    public string Name { get; private set; }
    public string Description { get; private set; }

    public Guid LanguagePairId { get; private set; }
    public LanguagePair LanguagePair { get; private set; } = null!;

    private LearningTrack() { Name = string.Empty; Description = string.Empty; }

    public LearningTrack(string name, string description, LanguagePair languagePair)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Track name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(languagePair);

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        LanguagePairId = languagePair.Id;
        LanguagePair = languagePair;
    }
}
