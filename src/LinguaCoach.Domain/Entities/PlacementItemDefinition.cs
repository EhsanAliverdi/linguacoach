using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// Admin-configurable placement assessment item, replacing the previously hardcoded
/// in-code item bank. Mirrors the OnboardingStepDefinition admin-CRUD pattern.
///
/// Form.io-native authoring (post-migration): every item is authored directly via the
/// Form.io builder (<see cref="FormIoSchemaJson"/>) with a backend-only scoring artifact
/// (<see cref="ScoringRulesJson"/>) keyed by Form.io component key. The legacy
/// QuestionContent-based authoring path (CorrectAnswer/ReadingPassage/ListeningAudioScript/
/// ContentJson) has been retired.
/// </summary>
public sealed class PlacementItemDefinition : BaseEntity
{
    public string Skill { get; private set; } = string.Empty;
    public string CefrLevel { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = string.Empty;
    public string Prompt { get; private set; } = string.Empty;

    public int ItemOrder { get; private set; }
    public bool IsEnabled { get; private set; }

    /// <summary>Student-safe Form.io schema for this item — never contains a correct answer or
    /// scoring data. Required for every item under the Form.io-native authoring model.</summary>
    public string? FormIoSchemaJson { get; private set; }

    /// <summary>Backend-only: correct answer(s)/rubric for this item, keyed by Form.io component
    /// key. Never returned to students.</summary>
    public string? ScoringRulesJson { get; private set; }

    /// <summary>Incremented every time ScoringRulesJson changes — copied onto issued
    /// PlacementAssessmentItem rows as a snapshot reference so a later scoring-rule edit never
    /// silently reinterprets a historical answer.</summary>
    public int ScoringRulesVersion { get; private set; }

    /// <summary>Which rendering engine FormIoSchemaJson is authored for. Only FormIo exists
    /// today; kept alongside the schema so a future alternate renderer doesn't need a further
    /// migration to distinguish items.</summary>
    public FormRendererKind RendererKind { get; private set; } = FormRendererKind.FormIo;

    public void SetFormIoAuthoring(string? formIoSchemaJson, string? scoringRulesJson, FormRendererKind rendererKind = FormRendererKind.FormIo)
    {
        FormIoSchemaJson = formIoSchemaJson;
        if (!string.Equals(ScoringRulesJson, scoringRulesJson, StringComparison.Ordinal))
            ScoringRulesVersion++;
        ScoringRulesJson = scoringRulesJson;
        RendererKind = rendererKind;
    }

    private PlacementItemDefinition() { }

    public PlacementItemDefinition(
        string skill,
        string cefrLevel,
        string itemType,
        string prompt,
        int itemOrder,
        bool isEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(itemType)) throw new ArgumentException("ItemType is required.", nameof(itemType));
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt is required.", nameof(prompt));

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemType = itemType.Trim();
        Prompt = prompt.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
    }

    public void Update(
        string skill,
        string cefrLevel,
        string itemType,
        string prompt,
        int itemOrder,
        bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(skill)) throw new ArgumentException("Skill is required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(cefrLevel)) throw new ArgumentException("CefrLevel is required.", nameof(cefrLevel));
        if (string.IsNullOrWhiteSpace(itemType)) throw new ArgumentException("ItemType is required.", nameof(itemType));
        if (string.IsNullOrWhiteSpace(prompt)) throw new ArgumentException("Prompt is required.", nameof(prompt));

        Skill = skill.Trim();
        CefrLevel = cefrLevel.Trim();
        ItemType = itemType.Trim();
        Prompt = prompt.Trim();
        ItemOrder = itemOrder;
        IsEnabled = isEnabled;
    }
}
