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

    // Phase 14A — Student placement gate configuration
    public bool PlacementRequiredBeforeLearning { get; set; } = true;
    public bool AllowSkipPlacement { get; set; } = false;
    public bool AllowPlacementRetake { get; set; } = false;
    public bool ResumeInterruptedPlacement { get; set; } = true;
    public bool AutoStartPlacement { get; set; } = false;
}
