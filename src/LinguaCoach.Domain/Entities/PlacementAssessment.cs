using LinguaCoach.Domain.Common;
using LinguaCoach.Domain.Enums;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A student's placement assessment. Standalone entity — NOT a LearningModule.
/// Holds the per-section flow state and the final AI-evaluated result.
/// See: docs/architecture/placement-assessment-model.md
/// </summary>
public sealed class PlacementAssessment : BaseEntity
{
    public Guid StudentProfileId { get; private set; }
    public PlacementStatus Status { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>Key of the section the student is currently on (e.g. "self_check").</summary>
    public string CurrentSectionKey { get; private set; }

    /// <summary>Serialised PlacementResult JSON (null until completed).</summary>
    public string? ResultJson { get; private set; }

    /// <summary>Estimated overall CEFR level from the result (null until completed).</summary>
    public string? OverallEstimatedLevel { get; private set; }

    /// <summary>Serialised per-skill CEFR levels from the result (null until completed).</summary>
    public string? SkillLevelsJson { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    // Phase 13A — Adaptive Placement Engine
    public DateTime? AbandonedAtUtc { get; private set; }
    public DateTime? ExpiredAtUtc { get; private set; }
    public double? OverallConfidence { get; private set; }
    public bool IsProvisional { get; private set; }
    public string? ResultSummary { get; private set; }
    public string? Source { get; private set; }
    public bool IsAdaptive { get; private set; }

    // Navigation — answers collected across all sections.
    private readonly List<PlacementAnswer> _answers = new();
    public IReadOnlyCollection<PlacementAnswer> Answers => _answers.AsReadOnly();

    // Navigation — adaptive assessment items (Phase 13A).
    private readonly List<PlacementAssessmentItem> _items = new();
    public IReadOnlyCollection<PlacementAssessmentItem> Items => _items.AsReadOnly();

    private PlacementAssessment()
    {
        CurrentSectionKey = string.Empty;
    }

    public PlacementAssessment(Guid studentProfileId, string firstSectionKey)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        if (string.IsNullOrWhiteSpace(firstSectionKey))
            throw new ArgumentException("First section key is required.", nameof(firstSectionKey));

        StudentProfileId = studentProfileId;
        Status = PlacementStatus.NotStarted;
        CurrentSectionKey = firstSectionKey;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory for Phase 13A adaptive assessments.
    /// </summary>
    public static PlacementAssessment CreateAdaptive(Guid studentProfileId, string source)
    {
        if (studentProfileId == Guid.Empty)
            throw new ArgumentException("StudentProfileId must not be empty.", nameof(studentProfileId));
        return new PlacementAssessment
        {
            StudentProfileId = studentProfileId,
            Status = PlacementStatus.NotStarted,
            CurrentSectionKey = "adaptive",
            UpdatedAtUtc = DateTime.UtcNow,
            IsAdaptive = true,
            Source = source
        };
    }

    public void Start()
    {
        if (Status == PlacementStatus.Completed)
            throw new InvalidOperationException("Placement is already completed.");

        if (Status == PlacementStatus.NotStarted)
        {
            Status = PlacementStatus.InProgress;
            StartedAtUtc = DateTime.UtcNow;
        }
        Touch();
    }

    /// <summary>
    /// Advances the current section pointer after a section's answers have been saved.
    /// Persistence of <see cref="PlacementAnswer"/> rows is handled by the application layer
    /// (so EF tracks inserts/deletes correctly). Idempotent per section.
    /// </summary>
    public void AdvanceSection(string sectionKey, string? nextSectionKey)
    {
        if (Status == PlacementStatus.Completed)
            throw new InvalidOperationException("Placement is already completed.");
        if (string.IsNullOrWhiteSpace(sectionKey))
            throw new ArgumentException("Section key is required.", nameof(sectionKey));

        if (Status == PlacementStatus.NotStarted)
            Start();

        if (!string.IsNullOrWhiteSpace(nextSectionKey))
            CurrentSectionKey = nextSectionKey;

        Touch();
    }

    public void Complete(string resultJson, string? overallEstimatedLevel, string? skillLevelsJson)
    {
        if (Status == PlacementStatus.Completed)
            throw new InvalidOperationException("Placement is already completed.");
        if (string.IsNullOrWhiteSpace(resultJson))
            throw new ArgumentException("Result JSON is required.", nameof(resultJson));

        Status = PlacementStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        ResultJson = resultJson;
        OverallEstimatedLevel = overallEstimatedLevel;
        SkillLevelsJson = skillLevelsJson;
        Touch();
    }

    // Phase 13A — Adaptive lifecycle methods

    public void Abandon()
    {
        if (Status != PlacementStatus.InProgress)
            throw new InvalidOperationException("Only in-progress assessments can be abandoned.");
        Status = PlacementStatus.Abandoned;
        AbandonedAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void Expire()
    {
        if (Status == PlacementStatus.Completed || Status == PlacementStatus.Abandoned)
            throw new InvalidOperationException($"Cannot expire assessment in status {Status}.");
        Status = PlacementStatus.Expired;
        ExpiredAtUtc = DateTime.UtcNow;
        Touch();
    }

    public void CompleteAdaptive(string? overallCefrLevel, double confidence, string? resultSummary, bool isProvisional)
    {
        if (Status == PlacementStatus.Completed)
            throw new InvalidOperationException("Placement is already completed.");
        if (Status == PlacementStatus.Abandoned)
            throw new InvalidOperationException("Cannot complete an abandoned assessment.");
        Status = PlacementStatus.Completed;
        CompletedAtUtc = DateTime.UtcNow;
        OverallEstimatedLevel = overallCefrLevel;
        OverallConfidence = confidence;
        ResultSummary = resultSummary;
        IsProvisional = isProvisional;
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
