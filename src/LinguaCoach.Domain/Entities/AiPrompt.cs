using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Versioned AI prompt template stored in the database.
/// Schema placeholder — fields will evolve when AI provider is confirmed.
/// </summary>
public sealed class AiPrompt : BaseEntity
{
    public string Key { get; private set; }
    public string Content { get; private set; }
    public int Version { get; private set; }
    public bool IsActive { get; private set; }

    private AiPrompt() { Key = string.Empty; Content = string.Empty; }

    public AiPrompt(string key, string content, int version = 1)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Prompt key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Prompt content is required.", nameof(content));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "Prompt version must be >= 1.");

        Key = key.Trim();
        Content = content.Trim();
        Version = version;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
