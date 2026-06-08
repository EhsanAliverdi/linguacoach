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

    // Navigation — answers collected across all sections.
    private readonly List<PlacementAnswer> _answers = new();
    public IReadOnlyCollection<PlacementAnswer> Answers => _answers.AsReadOnly();

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

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}
