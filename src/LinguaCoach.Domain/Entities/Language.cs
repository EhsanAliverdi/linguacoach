using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Represents a natural language supported by the platform.
/// Code follows ISO 639-1 (e.g. "fa" for Persian, "en" for English).
/// </summary>
public sealed class Language : BaseEntity
{
    public string Code { get; private set; }
    public string Name { get; private set; }
    public LanguageDirection Direction { get; private set; }

    private Language() { Code = string.Empty; Name = string.Empty; }

    public Language(string code, string name, LanguageDirection direction)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Language code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Language name is required.", nameof(name));

        Code = code.Trim().ToLowerInvariant();
        Name = name.Trim();
        Direction = direction;
    }
}
