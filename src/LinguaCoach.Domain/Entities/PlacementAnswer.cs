using LinguaCoach.Domain.Common;

namespace LinguaCoach.Domain.Entities;

/// <summary>
/// A single answer recorded during a placement assessment section.
/// Either <see cref="ResponseText"/> (free text / writing / speaking transcript)
/// or <see cref="SelectedOption"/> (MCQ / slider value) is set, depending on the section.
/// See: docs/architecture/placement-assessment-model.md
/// </summary>
public sealed class PlacementAnswer : BaseEntity
{
    public Guid PlacementAssessmentId { get; private set; }
    public string SectionKey { get; private set; }
    public string QuestionKey { get; private set; }
    public string? ResponseText { get; private set; }
    public string? SelectedOption { get; private set; }

    /// <summary>Deterministic per-answer score (0-100) where applicable; null for unscored sections.</summary>
    public double? Score { get; private set; }

    private PlacementAnswer()
    {
        SectionKey = string.Empty;
        QuestionKey = string.Empty;
    }

    public PlacementAnswer(
        Guid placementAssessmentId,
        string sectionKey,
        string questionKey,
        string? responseText,
        string? selectedOption,
        double? score)
    {
        if (string.IsNullOrWhiteSpace(sectionKey))
            throw new ArgumentException("Section key is required.", nameof(sectionKey));
        if (string.IsNullOrWhiteSpace(questionKey))
            throw new ArgumentException("Question key is required.", nameof(questionKey));

        PlacementAssessmentId = placementAssessmentId;
        SectionKey = sectionKey.Trim();
        QuestionKey = questionKey.Trim();
        ResponseText = responseText?.Trim();
        SelectedOption = selectedOption?.Trim();
        Score = score;
    }

    public void SetScore(double? score) => Score = score;
}
