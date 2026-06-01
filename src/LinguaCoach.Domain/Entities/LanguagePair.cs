using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A supported source→target language combination.
/// Seed: Persian (fa, RTL) → English (en, LTR).
/// </summary>
public sealed class LanguagePair : BaseEntity
{
    public Guid SourceLanguageId { get; private set; }
    public Language SourceLanguage { get; private set; } = null!;

    public Guid TargetLanguageId { get; private set; }
    public Language TargetLanguage { get; private set; } = null!;

    public bool IsActive { get; private set; }

    private LanguagePair() { }

    public LanguagePair(Language sourceLanguage, Language targetLanguage, bool isActive = true)
    {
        ArgumentNullException.ThrowIfNull(sourceLanguage);
        ArgumentNullException.ThrowIfNull(targetLanguage);

        if (sourceLanguage.Code == targetLanguage.Code)
            throw new ArgumentException("Source and target languages must be different.");

        SourceLanguageId = sourceLanguage.Id;
        SourceLanguage = sourceLanguage;
        TargetLanguageId = targetLanguage.Id;
        TargetLanguage = targetLanguage;
        IsActive = isActive;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
