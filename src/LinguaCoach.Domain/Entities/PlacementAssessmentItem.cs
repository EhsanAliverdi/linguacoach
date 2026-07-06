using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single question/item within an adaptive placement assessment (Phase 13A).
/// Form.io-native: the item's schema/scoring are snapshotted from its source
/// PlacementItemDefinition at issuance and the student's raw Form.io submission is stored
/// verbatim (SubmissionDataJson) alongside a per-component normalized view (NormalizedAnswerJson).
/// </summary>
public sealed class PlacementAssessmentItem : BaseEntity
{
    public Guid PlacementAssessmentId { get; private set; }

    /// <summary>FK to the PlacementItemDefinition this item was issued from — stable dedup
    /// identity for "has this item already been issued in this assessment".</summary>
    public Guid? SourceItemDefinitionId { get; private set; }

    public string Skill { get; private set; } = string.Empty;
    public string TargetCefrLevel { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = string.Empty;
    public string Prompt { get; private set; } = string.Empty;
    public double? Score { get; private set; }
    public bool? IsCorrect { get; private set; }
    public DateTime? EvaluatedAtUtc { get; private set; }
    public int ItemOrder { get; private set; }

    /// <summary>Seconds the candidate spent on this item, if captured by the client.</summary>
    public int? DurationSeconds { get; private set; }

    /// <summary>Storage key for the generated listening audio, once EnsureAudioAsync has run — null until then.</summary>
    public string? AudioStorageKey { get; private set; }
    public string? AudioContentType { get; private set; }

    /// <summary>Student-safe Form.io schema for this item, copied from PlacementItemDefinition at issuance.</summary>
    public string? FormIoSchemaJson { get; private set; }

    /// <summary>Backend-only: the scoring rules snapshot copied from the source
    /// PlacementItemDefinition at issuance, so a later edit to the live item's scoring rules
    /// never reinterprets this already-issued item's correctness. Never returned to students.</summary>
    public string? ScoringRulesJsonSnapshot { get; private set; }

    /// <summary>The ScoringRulesVersion of the source definition at the moment this item was issued.</summary>
    public int? ScoringRulesVersionSnapshot { get; private set; }

    /// <summary>Raw Form.io submission.data the student submitted, keyed by component key.
    /// Backend-only in the sense that it isn't shown back to the student redacted, but it is the
    /// student's own answer so there's no leak concern.</summary>
    public string? SubmissionDataJson { get; private set; }

    /// <summary>Per-component normalized value used for scoring (trimmed/case-folded text, or the
    /// normalized choice key(s)), set when RecordResponse runs.</summary>
    public string? NormalizedAnswerJson { get; private set; }

    private PlacementAssessmentItem() { }

    public static PlacementAssessmentItem Create(
        Guid assessmentId,
        string skill,
        string targetCefrLevel,
        string itemType,
        string prompt,
        int itemOrder,
        Guid? sourceItemDefinitionId = null,
        string? formIoSchemaJson = null,
        string? scoringRulesJsonSnapshot = null,
        int? scoringRulesVersionSnapshot = null)
    {
        if (assessmentId == Guid.Empty)
            throw new ArgumentException("AssessmentId required.", nameof(assessmentId));
        if (string.IsNullOrWhiteSpace(skill))
            throw new ArgumentException("Skill required.", nameof(skill));
        if (string.IsNullOrWhiteSpace(targetCefrLevel))
            throw new ArgumentException("TargetCefrLevel required.", nameof(targetCefrLevel));

        return new PlacementAssessmentItem
        {
            PlacementAssessmentId = assessmentId,
            SourceItemDefinitionId = sourceItemDefinitionId,
            Skill = skill,
            TargetCefrLevel = targetCefrLevel,
            ItemType = itemType,
            Prompt = prompt,
            ItemOrder = itemOrder,
            FormIoSchemaJson = formIoSchemaJson,
            ScoringRulesJsonSnapshot = scoringRulesJsonSnapshot,
            ScoringRulesVersionSnapshot = scoringRulesVersionSnapshot,
        };
    }

    public void RecordAudio(string storageKey, string contentType)
    {
        AudioStorageKey = storageKey;
        AudioContentType = contentType;
    }

    public void RecordResponse(
        string submissionDataJson, string? normalizedAnswerJson, bool isCorrect, double score, int? durationSeconds = null)
    {
        if (IsCorrect.HasValue)
            throw new InvalidOperationException("Response already recorded for this item.");

        SubmissionDataJson = submissionDataJson;
        NormalizedAnswerJson = normalizedAnswerJson;
        IsCorrect = isCorrect;
        Score = score;
        DurationSeconds = durationSeconds;
        EvaluatedAtUtc = DateTime.UtcNow;
    }
}
