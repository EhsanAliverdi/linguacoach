using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single question/item within an adaptive placement assessment (Phase 13A).
/// </summary>
public sealed class PlacementAssessmentItem : BaseEntity
{
    public Guid PlacementAssessmentId { get; private set; }
    public string Skill { get; private set; } = string.Empty;
    public string TargetCefrLevel { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = string.Empty;
    public string Prompt { get; private set; } = string.Empty;
    public string? Response { get; private set; }
    public double? Score { get; private set; }
    public bool? IsCorrect { get; private set; }
    public DateTime? EvaluatedAtUtc { get; private set; }
    public int ItemOrder { get; private set; }

    /// <summary>Correct answer stored for deterministic evaluation.</summary>
    public string? CorrectAnswer { get; private set; }

    /// <summary>Human-readable scoring note (e.g. "Expected: 'A'. Received: 'B'.").</summary>
    public string? EvaluationNotes { get; private set; }

    /// <summary>Seconds the candidate spent on this item, if captured by the client.</summary>
    public int? DurationSeconds { get; private set; }

    private PlacementAssessmentItem() { }

    public static PlacementAssessmentItem Create(
        Guid assessmentId,
        string skill,
        string targetCefrLevel,
        string itemType,
        string prompt,
        string? correctAnswer,
        int itemOrder)
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
            Skill = skill,
            TargetCefrLevel = targetCefrLevel,
            ItemType = itemType,
            Prompt = prompt,
            CorrectAnswer = correctAnswer,
            ItemOrder = itemOrder
        };
    }

    public void RecordResponse(string response, bool isCorrect, double score,
        string? evaluationNotes = null, int? durationSeconds = null)
    {
        if (IsCorrect.HasValue)
            throw new InvalidOperationException("Response already recorded for this item.");
        Response = response;
        IsCorrect = isCorrect;
        Score = score;
        EvaluationNotes = evaluationNotes;
        DurationSeconds = durationSeconds;
        EvaluatedAtUtc = DateTime.UtcNow;
    }
}
