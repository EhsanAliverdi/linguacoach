namespace LinguaCoach.Application.Placement;

public sealed class PlacementAssessmentOptions
{
    public const string Section = "PlacementAssessment";
    public int MinItems { get; set; } = 5;
    public int MaxItems { get; set; } = 20;
    public double ConfidenceThreshold { get; set; } = 0.75;
    public string[] SkillsToAssess { get; set; } = ["listening", "reading", "writing", "vocabulary", "grammar", "speaking"];
    public string StartingLevelFallback { get; set; } = "A2";
    public int AssessmentExpiryMinutes { get; set; } = 60;
}
