using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A role-specific career context for learning content.
/// Seed: "Document Controller" for the Persian→English / Workplace English track.
/// CareerProfile is scoped to a LanguagePair — when new language pairs are added,
/// profiles are duplicated or migrated (accepted tradeoff for first slice).
/// </summary>
public sealed class CareerProfile : BaseEntity
{
    public string Name { get; private set; }
    public string Description { get; private set; }

    public Guid LanguagePairId { get; private set; }
    public LanguagePair LanguagePair { get; private set; } = null!;

    private CareerProfile() { Name = string.Empty; Description = string.Empty; }

    public CareerProfile(string name, string description, LanguagePair languagePair)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Career profile name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(languagePair);

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        LanguagePairId = languagePair.Id;
        LanguagePair = languagePair;
    }
}
