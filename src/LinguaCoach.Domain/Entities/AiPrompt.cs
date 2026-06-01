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

    // Per-feature token budgets enforced by AiContextBuilder before every call.
    // Null = no explicit limit (discouraged; all production prompts should set these).
    public int? MaxInputTokens { get; private set; }
    public int? MaxOutputTokens { get; private set; }

    private AiPrompt() { Key = string.Empty; Content = string.Empty; }

    public AiPrompt(string key, string content, int version = 1, int? maxInputTokens = null, int? maxOutputTokens = null)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Prompt key is required.", nameof(key));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Prompt content is required.", nameof(content));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "Prompt version must be >= 1.");
        if (maxInputTokens is < 1) throw new ArgumentOutOfRangeException(nameof(maxInputTokens), "MaxInputTokens must be >= 1.");
        if (maxOutputTokens is < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens), "MaxOutputTokens must be >= 1.");

        Key = key.Trim();
        Content = content.Trim();
        Version = version;
        IsActive = true;
        MaxInputTokens = maxInputTokens;
        MaxOutputTokens = maxOutputTokens;
    }

    public void SetTokenBudget(int maxInputTokens, int maxOutputTokens)
    {
        if (maxInputTokens < 1) throw new ArgumentOutOfRangeException(nameof(maxInputTokens), "MaxInputTokens must be >= 1.");
        if (maxOutputTokens < 1) throw new ArgumentOutOfRangeException(nameof(maxOutputTokens), "MaxOutputTokens must be >= 1.");
        MaxInputTokens = maxInputTokens;
        MaxOutputTokens = maxOutputTokens;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
