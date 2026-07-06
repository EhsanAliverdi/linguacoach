using System.Text.Json;
using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;
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

    /// <summary>Student-safe Form.io schema for this item (Form.io item-bank authoring) — never
    /// contains a correct answer or scoring data. Null for items not yet re-authored via the
    /// Form.io builder; the adaptive engine falls back to mapping Content -> Form.io schema
    /// server-side for those.</summary>
    public string? FormIoSchemaJson { get; private set; }

    /// <summary>Backend-only: correct answer(s)/rubric for this item, keyed by Form.io component
    /// key. Never returned to students.</summary>
    public string? ScoringRulesJson { get; private set; }

    /// <summary>Which rendering engine FormIoSchemaJson is authored for. Only FormIo exists
    /// today; kept alongside the schema so a future alternate renderer doesn't need a further
    /// migration to distinguish items.</summary>
    public FormRendererKind RendererKind { get; private set; } = FormRendererKind.FormIo;

    public void SetFormIoAuthoring(string? formIoSchemaJson, string? scoringRulesJson, FormRendererKind rendererKind = FormRendererKind.FormIo)
    {
        FormIoSchemaJson = formIoSchemaJson;
        ScoringRulesJson = scoringRulesJson;
        RendererKind = rendererKind;
    }

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
