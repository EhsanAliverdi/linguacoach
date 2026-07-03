using System.Text.Json;
using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Questions;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Admin-configurable placement assessment item, replacing the previously hardcoded
/// in-code item bank. Mirrors the OnboardingStepDefinition admin-CRUD pattern.
/// </summary>
public sealed class PlacementItemDefinition : BaseEntity
{
    public string Skill { get; private set; } = string.Empty;
    public string CefrLevel { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = string.Empty;
    public string Prompt { get; private set; } = string.Empty;
    public string CorrectAnswer { get; private set; } = string.Empty;

    // Optional richer content: a full passage for reading items, a script to be
    // converted to speech (via PlacementAudioService) for listening items.
    public string? ReadingPassage { get; private set; }
    public string? ListeningAudioScript { get; private set; }

    public int ItemOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>Unified question schema (Phase 2) snapshot of this item's content, kept in sync
    /// with the legacy flat fields above until they're dropped (Phase 7). Null only for rows
    /// created before this field existed and not yet backfilled.</summary>
    public string? ContentJson { get; private set; }

    public QuestionContent? Content => QuestionContentJson.TryDeserializeContent(ContentJson);

    public void SetContent(QuestionContent content) =>
        ContentJson = JsonSerializer.Serialize<QuestionContent>(content);

    private PlacementItemDefinition() { }

    public PlacementItemDefinition(
        string skill,
        string cefrLevel,
        string itemType,
        string prompt,
        string correctAnswer,
        int itemOrder,
        bool isEnabled = true,
        string? readingPassage = null,
        string? listeningAudioScript = null)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(itemType)) throw new ArgumentException("ItemType is required.", nameof(itemType));
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt is required.", nameof(prompt));
        if (string.IsNullOrWhiteSpace(correctAnswer)) throw new ArgumentException("CorrectAnswer is required.", nameof(correctAnswer));

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemType = itemType.Trim();
        Prompt = prompt.Trim();
        CorrectAnswer = correctAnswer.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
        ReadingPassage = string.IsNullOrWhiteSpace(readingPassage) ? null : readingPassage.Trim();
        ListeningAudioScript = string.IsNullOrWhiteSpace(listeningAudioScript) ? null : listeningAudioScript.Trim();
    }

    public void Update(
        string skill,
        string cefrLevel,
        string itemType,
        string prompt,
        string correctAnswer,
        int itemOrder,
        bool isEnabled,
        string? readingPassage,
        string? listeningAudioScript)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(itemType)) throw new ArgumentException("ItemType is required.", nameof(itemType));
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt is required.", nameof(prompt));
        if (string.IsNullOrWhiteSpace(correctAnswer)) throw new ArgumentException("CorrectAnswer is required.", nameof(correctAnswer));

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemType = itemType.Trim();
        Prompt = prompt.Trim();
        CorrectAnswer = correctAnswer.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
        ReadingPassage = string.IsNullOrWhiteSpace(readingPassage) ? null : readingPassage.Trim();
        ListeningAudioScript = string.IsNullOrWhiteSpace(listeningAudioScript) ? null : listeningAudioScript.Trim();
    }
}
